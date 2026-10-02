using Microsoft.EntityFrameworkCore;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Application.Common.Time;
using RentApp.Application.Payments;
using RentApp.Domain.Rent;
using RentApp.Domain.Users;

namespace RentApp.Application.Rent;

/// <summary>
/// Reading rent dues (ViewTenants) and owner-only actions: generating charges and waiving amounts.
/// Status filters are evaluated in SQL against today in the organization's time zone.
/// </summary>
public sealed class RentService(
    IAppDbContext db,
    AuditWriter audit,
    ICurrentUser currentUser,
    OrganizationClock calendar,
    RentChargeGenerator generator,
    TimeProvider clock)
{
    public const string AdjustedAction = "rent.adjusted";

    public async Task<PagedResult<RentChargeDto>> ListAsync(RentChargeQuery query, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewTenants);
        var today = await calendar.TodayAsync(cancellationToken);

        var charges = Filter(db.RentCharges.AsNoTracking(), query.Filter, today);
        if (query.PropertyId is { } propertyId) charges = charges.Where(c => c.PropertyId == propertyId);
        if (query.TenantId is { } tenantId) charges = charges.Where(c => c.TenantId == tenantId);
        if (query.Month is { } month)
        {
            var periodStart = RentSchedule.MonthStart(month);
            charges = charges.Where(c => c.PeriodStart == periodStart);
        }

        var total = await charges.CountAsync(cancellationToken);
        // Money owed reads oldest-first (most overdue on top); history reads newest-first.
        var ordered = query.Filter is RentFilter.Paid or RentFilter.All
            ? charges.OrderByDescending(c => c.DueDate).ThenBy(c => c.Id)
            : charges.OrderBy(c => c.DueDate).ThenBy(c => c.Id);
        var page = await Project(ordered.Skip(query.Skip).Take(query.PageSize), today).ToListAsync(cancellationToken);
        return new PagedResult<RentChargeDto>([.. page.Select(r => r.ToDto(today))], query.Page, query.PageSize, total);
    }

    public Task<PagedResult<RentChargeDto>> OverdueAsync(PageQuery page, Guid? propertyId, CancellationToken cancellationToken) =>
        ListAsync(new RentChargeQuery { Filter = RentFilter.Overdue, Page = page.Page, PageSize = page.PageSize, PropertyId = propertyId }, cancellationToken);

    public async Task<RentChargeDetail> GetAsync(Guid chargeId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewTenants);
        var today = await calendar.TodayAsync(cancellationToken);
        var row = await Project(db.RentCharges.AsNoTracking().Where(c => c.Id == chargeId), today).SingleOrDefaultAsync(cancellationToken)
                  ?? throw RentErrors.ChargeNotFound();
        var adjustments = await db.RentChargeAdjustments.AsNoTracking()
            .Where(a => a.RentChargeId == chargeId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new RentAdjustmentDto(a.Id, a.Amount, a.Reason, a.CreatedAt))
            .ToListAsync(cancellationToken);
        var payments = await (
                from a in db.PaymentAllocations.AsNoTracking()
                join p in db.Payments on a.PaymentId equals p.Id
                join r in db.Receipts on p.Id equals r.PaymentId
                where a.RentChargeId == chargeId
                orderby p.PaymentDate, p.CreatedAt
                select new ChargePaymentDto(p.Id, p.PaymentDate, p.Method, a.AllocatedAmount, p.Status, r.Id, r.ReceiptNumber))
            .ToListAsync(cancellationToken);
        return new RentChargeDetail(row.ToDto(today), adjustments, payments);
    }

    public async Task<RentSummary> SummaryAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewTenants);
        var today = await calendar.TodayAsync(cancellationToken);
        var weekEnd = today.AddDays(6);
        var monthStart = RentSchedule.MonthStart(today);

        var live = db.RentCharges.Where(c => c.CancelledAt == null && (propertyId == null || c.PropertyId == propertyId));
        var outstanding = live.Where(c => c.BalanceAmount > 0);

        return new RentSummary(
            today,
            await TotalAsync(outstanding, c => c.BalanceAmount, cancellationToken),
            await TotalAsync(outstanding.Where(c => c.DueDate < today), c => c.BalanceAmount, cancellationToken),
            await TotalAsync(outstanding.Where(c => c.DueDate == today), c => c.BalanceAmount, cancellationToken),
            await TotalAsync(outstanding.Where(c => c.DueDate >= today && c.DueDate <= weekEnd), c => c.BalanceAmount, cancellationToken),
            await TotalAsync(live.Where(c => c.PeriodStart == monthStart), c => c.Amount, cancellationToken));
    }

    public async Task<GenerateResult> GenerateAsync(CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        return new GenerateResult(await generator.GenerateAsync(tenantIds: null, cancellationToken));
    }

    /// <summary>
    /// Waives part or all of a charge's balance, with a reason. The balance check and the update are one
    /// conditional SQL statement, so concurrent waivers or payments can never push a charge below zero.
    /// </summary>
    public async Task<RentChargeDetail> AdjustAsync(Guid chargeId, AdjustChargeRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var amount = request.Amount!.Value;
        var charge = await db.RentCharges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == chargeId, cancellationToken)
                     ?? throw RentErrors.ChargeNotFound();
        if (charge.IsCancelled)
        {
            throw RentErrors.ChargeCancelled();
        }

        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.RentCharges
            .Where(c => c.Id == chargeId && c.CancelledAt == null && c.Amount - c.PaidAmount - c.AdjustedAmount >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AdjustedAmount, c => c.AdjustedAmount + amount)
                .SetProperty(c => c.UpdatedAt, now), cancellationToken);
        if (updated == 0)
        {
            throw RentErrors.AdjustmentTooLarge();
        }

        var adjustment = RentChargeAdjustment.Create(
            currentUser.OrganizationId, chargeId, amount, request.Reason, currentUser.IsAuthenticated ? currentUser.UserId : null, now);
        db.RentChargeAdjustments.Add(adjustment);
        audit.Record(AdjustedAction, nameof(RentCharge), chargeId, new { amount, reason = adjustment.Reason });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(chargeId, cancellationToken);
    }

    // ---- Query helpers ---------------------------------------------------------------------------

    private static IQueryable<RentCharge> Filter(IQueryable<RentCharge> charges, RentFilter filter, DateOnly today)
    {
        var live = charges.Where(c => c.CancelledAt == null);
        var outstanding = live.Where(c => c.BalanceAmount > 0);
        return filter switch
        {
            RentFilter.Outstanding => outstanding,
            RentFilter.Overdue => outstanding.Where(c => c.DueDate < today),
            RentFilter.DueToday => outstanding.Where(c => c.DueDate == today),
            RentFilter.Upcoming => outstanding.Where(c => c.DueDate > today),
            RentFilter.Paid => live.Where(c => c.BalanceAmount == 0),
            _ => live,
        };
    }

    private sealed record ChargeRow(
        RentCharge Charge, string TenantName, string TenantPhone, string PropertyName, string RoomNumber, string BedLabel)
    {
        public RentChargeDto ToDto(DateOnly today)
        {
            var c = Charge;
            return new RentChargeDto(
                c.Id, c.RentAgreementId, c.TenantId, TenantName, TenantPhone, c.PropertyId, PropertyName, RoomNumber, BedLabel,
                c.PeriodStart, c.PeriodEnd, c.DueDate, c.Amount, c.PaidAmount, c.AdjustedAmount, c.BalanceAmount,
                RentStatusRules.Derive(c.DueDate, c.PaidAmount, c.BalanceAmount, c.IsCancelled, today),
                RentStatusRules.DaysOverdue(c.DueDate, c.BalanceAmount, c.IsCancelled, today));
        }
    }

    private IQueryable<ChargeRow> Project(IQueryable<RentCharge> charges, DateOnly today) =>
        from c in charges
        join t in db.Tenants on c.TenantId equals t.Id
        join a in db.RentAgreements on c.RentAgreementId equals a.Id
        join p in db.Properties on c.PropertyId equals p.Id
        join r in db.Rooms on a.RoomId equals r.Id
        join b in db.Beds on a.BedId equals b.Id
        select new ChargeRow(c, t.FullName, t.Phone, p.Name, r.RoomNumber, b.Label);

    private static async Task<AmountCount> TotalAsync(
        IQueryable<RentCharge> charges, System.Linq.Expressions.Expression<Func<RentCharge, decimal>> amount, CancellationToken cancellationToken) =>
        new(await charges.SumAsync(amount, cancellationToken), await charges.CountAsync(cancellationToken));
}

internal static class RentErrors
{
    public static NotFoundException ChargeNotFound() => new("RENT_CHARGE_NOT_FOUND", "The requested rent charge was not found.");

    public static ConflictException ChargeCancelled() => new("RENT_CHARGE_CANCELLED", "This charge was cancelled and is no longer owed.");

    public static ValidationException AdjustmentTooLarge() =>
        new("ADJUSTMENT_EXCEEDS_BALANCE", "You cannot waive more than the amount still owed.",
            new Dictionary<string, string[]> { ["amount"] = ["You cannot waive more than the amount still owed."] });
}

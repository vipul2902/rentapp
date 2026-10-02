using Microsoft.EntityFrameworkCore;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Application.Common.Time;
using RentApp.Application.Rent;
using RentApp.Domain.Properties;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Application.Tenants;

/// <summary>
/// Tenants and their tenancies (move-in, move-out, moving beds). Viewing needs ViewTenants; changes are
/// owner-only. "One active tenancy per bed" and "one per tenant" are enforced by partial unique indexes,
/// so concurrent requests can never double-book a bed.
/// </summary>
public sealed class TenantService(
    IAppDbContext db, AuditWriter audit, ICurrentUser currentUser, OrganizationClock calendar, RentChargeGenerator rentCharges)
{
    private const int MoveInYearsBack = 10;

    public async Task<PagedResult<TenantSummary>> ListAsync(TenantListQuery query, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewTenants);
        var today = await calendar.TodayAsync(cancellationToken);

        var tenants = db.Tenants.AsNoTracking().Where(t => t.Status == TenantStatus.Active);
        var active = db.RentAgreements.Where(a => a.Status == AgreementStatus.Active);

        tenants = query.Filter switch
        {
            TenantFilter.Current => tenants.Where(t => active.Any(a => a.TenantId == t.Id)),
            TenantFilter.Former => tenants.Where(t =>
                !active.Any(a => a.TenantId == t.Id) && db.RentAgreements.Any(a => a.TenantId == t.Id)),
            TenantFilter.Unassigned => tenants.Where(t => !db.RentAgreements.Any(a => a.TenantId == t.Id)),
            TenantFilter.Overdue => tenants.Where(t => db.RentCharges.Any(c =>
                c.TenantId == t.Id && c.CancelledAt == null && c.BalanceAmount > 0 && c.DueDate < today)),
            _ => tenants,
        };

        if (query.PropertyId is { } propertyId)
        {
            tenants = tenants.Where(t => active.Any(a => a.TenantId == t.Id && a.PropertyId == propertyId));
        }

        if (query.RoomId is { } roomId)
        {
            tenants = tenants.Where(t => active.Any(a => a.TenantId == t.Id && a.RoomId == roomId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            var digits = Tenant.DigitsOnly(term);
            var searchPhone = digits.Length >= 3;
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL upper(...) LIKE; .NET culture rules do not apply.
            tenants = tenants.Where(t =>
                t.FullName.ToUpper().Contains(term)
                || (t.Email != null && t.Email.ToUpper().Contains(term))
                || (searchPhone && t.PhoneDigits.Contains(digits))
                || active.Any(a => a.TenantId == t.Id && db.Rooms.Any(r => r.Id == a.RoomId && r.RoomNumber.ToUpper() == term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await tenants.CountAsync(cancellationToken);
        var page = await tenants.OrderBy(t => t.FullName).ThenBy(t => t.Id)
            .Skip(query.Skip).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var ids = page.Select(t => t.Id).ToList();
        var current = await TenancyQueries.ForTenantsAsync(db, ids, activeOnly: true, today, cancellationToken);
        var balances = await BalancesAsync(ids, today, cancellationToken);
        var items = page
            .Select(t =>
            {
                var balance = balances.GetValueOrDefault(t.Id, TenantBalance.None);
                return new TenantSummary(t.Id, t.FullName, t.Phone, t.Email, t.Status, current[t.Id].FirstOrDefault(),
                    balance.Outstanding, balance.Overdue, t.CreatedAt);
            })
            .ToList();
        return new PagedResult<TenantSummary>(items, query.Page, query.PageSize, total);
    }

    public async Task<TenantDetail> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewTenants);
        var tenant = await FindAsync(tenantId, cancellationToken);
        return await ToDetailAsync(tenant, cancellationToken);
    }

    public async Task<TenantDetail> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var tenant = Tenant.Create(
            currentUser.OrganizationId, request.FullName, request.Phone, request.Email,
            request.EmergencyContactName, request.EmergencyContactPhone, request.PermanentAddress);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Tenants.Add(tenant);
        audit.Record(TenantAuditActions.TenantCreated, nameof(Tenant), tenant.Id);
        await db.SaveChangesAsync(cancellationToken);

        if (request.MoveIn is { } moveIn)
        {
            await MoveInCoreAsync(tenant, moveIn, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await ToDetailAsync(tenant, cancellationToken);
    }

    public async Task<TenantDetail> UpdateAsync(Guid tenantId, TenantDetailsRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var tenant = await FindEditableAsync(tenantId, cancellationToken);
        tenant.Update(request.FullName, request.Phone, request.Email, request.EmergencyContactName, request.EmergencyContactPhone, request.PermanentAddress);
        audit.Record(TenantAuditActions.TenantUpdated, nameof(Tenant), tenant.Id);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(tenant, cancellationToken);
    }

    /// <summary>Hides a tenant who no longer lives anywhere. Their history is kept.</summary>
    public async Task ArchiveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var tenant = await FindAsync(tenantId, cancellationToken);
        if (tenant.IsArchived)
        {
            return;
        }

        if (await ActiveAgreementAsync(tenant.Id, cancellationToken) is not null)
        {
            throw TenantErrors.HasActiveTenancy();
        }

        tenant.Archive();
        audit.Record(TenantAuditActions.TenantArchived, nameof(Tenant), tenant.Id);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TenantDetail> MoveInAsync(Guid tenantId, MoveInRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var tenant = await FindEditableAsync(tenantId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await MoveInCoreAsync(tenant, request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDetailAsync(tenant, cancellationToken);
    }

    /// <summary>
    /// Ends the tenant's stay; the bed becomes vacant. A booking whose move-in date has not arrived is
    /// cancelled instead (nothing is ever charged for it).
    /// </summary>
    public async Task<TenantDetail> MoveOutAsync(Guid tenantId, MoveOutRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var tenant = await FindEditableAsync(tenantId, cancellationToken);
        var agreement = await ActiveAgreementAsync(tenant.Id, cancellationToken) ?? throw TenantErrors.NoActiveTenancy();
        var today = await calendar.TodayAsync(cancellationToken);

        DateOnly? keepChargesUntil;
        if (agreement.StateOn(today) == TenancyState.Upcoming)
        {
            agreement.Cancel(today);
            keepChargesUntil = null; // a cancelled booking owes nothing
            audit.Record(TenantAuditActions.BookingCancelled, nameof(RentAgreement), agreement.Id, new { agreement.BedId });
        }
        else
        {
            var lastDay = request.MoveOutDate!.Value;
            if (lastDay > today)
            {
                throw TenantErrors.DateInFuture("moveOutDate", "move-out date");
            }

            if (lastDay < agreement.StartDate)
            {
                throw TenantErrors.DateBeforeMoveIn("moveOutDate");
            }

            agreement.End(lastDay, AgreementEndReason.MovedOut);
            keepChargesUntil = lastDay;
            audit.Record(TenantAuditActions.MovedOut, nameof(Tenant), tenant.Id, new { agreement.BedId, lastDay });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await rentCharges.CancelUnpaidAfterAsync(agreement.Id, keepChargesUntil, cancellationToken);
        await rentCharges.GenerateAsync([tenant.Id], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDetailAsync(tenant, cancellationToken);
    }

    /// <summary>Moves a tenant to another bed: ends the current tenancy and starts a new one with the same deposit and due day.</summary>
    public async Task<TenantDetail> MoveAsync(Guid tenantId, MoveTenantRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var tenant = await FindEditableAsync(tenantId, cancellationToken);
        var current = await ActiveAgreementAsync(tenant.Id, cancellationToken) ?? throw TenantErrors.NoActiveTenancy();
        if (request.BedId == current.BedId)
        {
            throw TenantErrors.SameBed();
        }

        var today = await calendar.TodayAsync(cancellationToken);
        var upcoming = current.StateOn(today) == TenancyState.Upcoming;
        var moveDate = upcoming ? current.StartDate : request.MoveDate!.Value;
        if (!upcoming)
        {
            if (moveDate > today)
            {
                throw TenantErrors.DateInFuture("moveDate", "move date");
            }

            if (moveDate < current.StartDate)
            {
                throw TenantErrors.DateBeforeMoveIn("moveDate");
            }
        }

        var target = await LoadRentableBedAsync(request.BedId!.Value, cancellationToken);
        var rent = request.MonthlyRent ?? target.Bed.DefaultMonthlyRent ?? current.MonthlyRent;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        DateOnly? keepChargesUntil;
        if (upcoming)
        {
            // A booking that has not started simply changes bed.
            current.Cancel(today);
            keepChargesUntil = null;
        }
        else
        {
            // The last night in the old bed is the day before the move (or the move-in day for same-day moves).
            var lastDay = moveDate > current.StartDate ? moveDate.AddDays(-1) : current.StartDate;
            current.End(lastDay, AgreementEndReason.Transferred);
            keepChargesUntil = lastDay;
        }

        // Saved first: the "one active tenancy per tenant" index must see the old one ended.
        await db.SaveChangesAsync(cancellationToken);

        ReleaseReservation(target.Bed);
        var next = RentAgreement.Start(
            currentUser.OrganizationId, tenant.Id, target.PropertyId, target.Bed.RoomId, target.Bed.Id,
            rent, current.SecurityDeposit, current.RentDueDay, moveDate);
        db.RentAgreements.Add(next);
        audit.Record(TenantAuditActions.Moved, nameof(Tenant), tenant.Id, new { fromBedId = current.BedId, toBedId = target.Bed.Id, moveDate });
        await SaveTenancyAsync(target.Bed.Label, cancellationToken);
        await rentCharges.CancelUnpaidAfterAsync(current.Id, keepChargesUntil, cancellationToken);
        await rentCharges.GenerateAsync([tenant.Id], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDetailAsync(tenant, cancellationToken);
    }

    public async Task<TenantDetail> UpdateTermsAsync(Guid tenantId, UpdateTenancyTermsRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var tenant = await FindEditableAsync(tenantId, cancellationToken);
        var agreement = await ActiveAgreementAsync(tenant.Id, cancellationToken) ?? throw TenantErrors.NoActiveTenancy();

        var before = new { agreement.MonthlyRent, agreement.SecurityDeposit, agreement.RentDueDay };
        agreement.UpdateTerms(request.MonthlyRent!.Value, request.SecurityDeposit, request.RentDueDay);
        audit.Record(TenantAuditActions.TermsUpdated, nameof(RentAgreement), agreement.Id, new
        {
            from = before,
            to = new { agreement.MonthlyRent, agreement.SecurityDeposit, agreement.RentDueDay },
        });
        await db.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(tenant, cancellationToken);
    }

    // ---- Internals -------------------------------------------------------------------------------

    private async Task MoveInCoreAsync(Tenant tenant, MoveInRequest request, CancellationToken cancellationToken)
    {
        if (await ActiveAgreementAsync(tenant.Id, cancellationToken) is not null)
        {
            throw TenantErrors.AlreadyAssigned();
        }

        var today = await calendar.TodayAsync(cancellationToken);
        var startDate = request.StartDate!.Value;
        if (startDate < today.AddYears(-MoveInYearsBack) || startDate > today.AddYears(1))
        {
            throw TenantErrors.MoveInOutOfRange();
        }

        var target = await LoadRentableBedAsync(request.BedId!.Value, cancellationToken);
        var rent = request.MonthlyRent ?? target.Bed.DefaultMonthlyRent ?? throw TenantErrors.RentRequired();

        ReleaseReservation(target.Bed);
        var agreement = RentAgreement.Start(
            currentUser.OrganizationId, tenant.Id, target.PropertyId, target.Bed.RoomId, target.Bed.Id,
            rent, request.SecurityDeposit, request.RentDueDay, startDate);
        db.RentAgreements.Add(agreement);
        audit.Record(TenantAuditActions.MovedIn, nameof(Tenant), tenant.Id, new { bedId = target.Bed.Id, startDate });
        await SaveTenancyAsync(target.Bed.Label, cancellationToken);

        // Backdated tenants get every month owed so far, straight away.
        await rentCharges.GenerateAsync([tenant.Id], cancellationToken);
    }

    private sealed record RentableBed(Bed Bed, Guid PropertyId);

    /// <summary>The bed must exist in this organization, be rentable, and have no active tenancy.</summary>
    private async Task<RentableBed> LoadRentableBedAsync(Guid bedId, CancellationToken cancellationToken)
    {
        var bed = await db.Beds.SingleOrDefaultAsync(b => b.Id == bedId, cancellationToken) ?? throw TenantErrors.BedNotFound();
        var room = await db.Rooms.SingleAsync(r => r.Id == bed.RoomId, cancellationToken);
        var property = await db.Properties.SingleAsync(p => p.Id == room.PropertyId, cancellationToken);

        if (property.IsArchived)
        {
            throw TenantErrors.PropertyArchived();
        }

        if (room.Status != RoomStatus.Active)
        {
            throw TenantErrors.RoomNotRentable(room.RoomNumber);
        }

        if (bed.Status is BedStatus.Unavailable or BedStatus.Archived)
        {
            throw TenantErrors.BedNotRentable(bed.Label);
        }

        if (await db.RentAgreements.AnyAsync(a => a.BedId == bed.Id && a.Status == AgreementStatus.Active, cancellationToken))
        {
            throw TenantErrors.BedOccupied(bed.Label);
        }

        return new RentableBed(bed, property.Id);
    }

    /// <summary>A manual "Reserved" hold is released once someone is actually assigned to the bed.</summary>
    private static void ReleaseReservation(Bed bed)
    {
        if (bed.Status == BedStatus.Reserved)
        {
            bed.Update(bed.Label, BedStatus.Available, bed.DefaultMonthlyRent);
        }
    }

    /// <summary>Saves a new tenancy, translating a lost race on the partial unique indexes into a clear error.</summary>
    private async Task SaveTenancyAsync(string bedLabel, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex)
        {
            throw ex.ConstraintName?.Contains("tenant_id", StringComparison.Ordinal) == true
                ? TenantErrors.AlreadyAssigned()
                : TenantErrors.BedOccupied(bedLabel);
        }
    }

    private Task<RentAgreement?> ActiveAgreementAsync(Guid tenantId, CancellationToken cancellationToken) =>
        db.RentAgreements.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.Status == AgreementStatus.Active, cancellationToken);

    private async Task<Tenant> FindAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken) ?? throw TenantErrors.TenantNotFound();

    private async Task<Tenant> FindEditableAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await FindAsync(tenantId, cancellationToken);
        return tenant.IsArchived ? throw TenantErrors.TenantArchived() : tenant;
    }

    private async Task<TenantDetail> ToDetailAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var today = await calendar.TodayAsync(cancellationToken);
        var history = await TenancyQueries.ForTenantsAsync(db, [tenant.Id], activeOnly: false, today, cancellationToken);
        var balance = (await BalancesAsync([tenant.Id], today, cancellationToken)).GetValueOrDefault(tenant.Id, TenantBalance.None);
        return TenantDetail.From(tenant, [.. history[tenant.Id]], balance);
    }

    /// <summary>What each tenant still owes, and how much of it is past due.</summary>
    private async Task<Dictionary<Guid, TenantBalance>> BalancesAsync(
        List<Guid> tenantIds, DateOnly today, CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return [];
        }

        var rows = await db.RentCharges
            .Where(c => tenantIds.Contains(c.TenantId) && c.CancelledAt == null && c.BalanceAmount > 0)
            .GroupBy(c => c.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Outstanding = g.Sum(c => c.BalanceAmount),
                Overdue = g.Where(c => c.DueDate < today).Sum(c => c.BalanceAmount),
            })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.TenantId, r => new TenantBalance(r.Outstanding, r.Overdue));
    }
}

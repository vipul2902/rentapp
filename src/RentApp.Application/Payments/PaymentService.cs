using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Application.Common.Time;
using RentApp.Application.Rent;
using RentApp.Domain.Payments;
using RentApp.Domain.Users;

namespace RentApp.Application.Payments;

/// <summary>
/// Recording and voiding payments. Financial integrity rules:
/// - All of a tenant's payment changes run one at a time (a per-tenant database lock), inside one transaction.
/// - Each charge's paid amount moves only through a conditional SQL update that re-checks the balance, so
///   concurrent payments and waivers can never over-settle a charge (and a database constraint backs this up).
/// - An Idempotency-Key makes a retried request return the original payment instead of recording it twice.
/// - The receipt number is taken from a counter in the same transaction, so numbers are unique and have no gaps.
/// </summary>
public sealed class PaymentService(
    IAppDbContext db,
    AuditWriter audit,
    ICurrentUser currentUser,
    OrganizationClock calendar,
    RentChargeGenerator generator,
    TimeProvider clock)
{
    public const string RecordedAction = "payment.recorded";
    public const string VoidedAction = "payment.voided";

    public async Task<RecordPaymentResult> RecordAsync(RecordPaymentRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.RecordPayments);
        var key = NormalizeKey(idempotencyKey);
        var tenantId = request.TenantId!.Value;
        var amount = request.Amount!.Value;

        if (key is not null && await FindByKeyAsync(key, cancellationToken) is { } earlier)
        {
            return await ReplayAsync(earlier, request, cancellationToken);
        }

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            throw PaymentErrors.TenantNotFound();
        }

        var today = await calendar.TodayAsync(cancellationToken);
        if (request.PaymentDate!.Value > today)
        {
            throw PaymentErrors.DateInFuture();
        }

        // A due that came into the 7-day window since the background job last ran is payable right away.
        await generator.GenerateAsync([tenantId], cancellationToken);

        try
        {
            var paymentId = await RecordLockedAsync(request, key, today, cancellationToken);
            return await ResultAsync(paymentId, cancellationToken);
        }
        catch (UniqueConstraintViolationException) when (key is not null)
        {
            // The same key was used concurrently for another tenant's payment; the first one won.
            db.ChangeTracker.Clear();
            var winner = await FindByKeyAsync(key, cancellationToken)
                         ?? throw new InvalidOperationException("Idempotency key conflict without a stored payment.");
            return await ReplayAsync(winner, request, cancellationToken);
        }
    }

    private async Task<Guid> RecordLockedAsync(RecordPaymentRequest request, string? key, DateOnly today, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId!.Value;
        var amount = request.Amount!.Value;
        var now = clock.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.AcquireOrganizationLockAsync(TenantLock(tenantId), cancellationToken);

        // Re-check under the lock: a retry of the same request may have committed while we waited.
        if (key is not null && await FindByKeyAsync(key, cancellationToken) is { } earlier)
        {
            EnsureSameRequest(earlier, request);
            return earlier.Id;
        }

        var charges = await PayableChargesAsync(tenantId, request.ChargeIds, cancellationToken);
        var owed = charges.Sum(c => c.Balance);
        if (amount > owed)
        {
            throw PaymentErrors.ExceedsOutstanding(owed);
        }

        var allocations = PaymentAllocator.Allocate(amount, charges.Select(c => new PaymentAllocator.Outstanding(c.Id, c.Balance)));
        var payment = Payment.Record(
            currentUser.OrganizationId, tenantId, request.PaymentDate!.Value, amount, request.Method!.Value,
            request.ReferenceNumber, request.Notes, ActorId, key, now);
        db.Payments.Add(payment);

        foreach (var allocation in allocations)
        {
            var share = allocation.Amount;
            var updated = await db.RentCharges
                .Where(c => c.Id == allocation.ChargeId && c.CancelledAt == null && c.Amount - c.PaidAmount - c.AdjustedAmount >= share)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.PaidAmount, c => c.PaidAmount + share)
                    .SetProperty(c => c.UpdatedAt, now), cancellationToken);
            if (updated == 0)
            {
                // A waiver or move-out changed this due after we read it. Nothing is saved.
                throw PaymentErrors.DuesChanged();
            }

            db.PaymentAllocations.Add(PaymentAllocation.Create(currentUser.OrganizationId, payment.Id, allocation.ChargeId, share));
        }

        var paid = charges.Where(c => allocations.Any(a => a.ChargeId == c.Id)).ToList();
        var details = await ReceiptDetailsAsync(tenantId, paid, cancellationToken);
        var sequence = await db.NextReceiptSequenceAsync(today.Year, cancellationToken);
        var receipt = Receipt.Issue(currentUser.OrganizationId, payment, sequence, today, details, now);
        db.Receipts.Add(receipt);

        audit.Record(RecordedAction, nameof(Payment), payment.Id, new
        {
            amount,
            method = payment.Method,
            paymentDate = payment.PaymentDate,
            receiptNumber = receipt.ReceiptNumber,
            allocations = allocations.Select(a => new { rentChargeId = a.ChargeId, amount = a.Amount }),
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return payment.Id;
    }

    /// <summary>
    /// Owner-only correction: the payment stays on record marked as voided, its amounts are taken off the
    /// charges it paid, and its receipt shows VOID. A payment can be voided once.
    /// </summary>
    public async Task<PaymentDto> VoidAsync(Guid paymentId, VoidPaymentRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner("Only the owner can void a payment.");
        var tenantId = await db.Payments.Where(p => p.Id == paymentId).Select(p => (Guid?)p.TenantId).SingleOrDefaultAsync(cancellationToken)
                       ?? throw PaymentErrors.PaymentNotFound();
        var now = clock.GetUtcNow();

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.AcquireOrganizationLockAsync(TenantLock(tenantId), cancellationToken);
            var payment = await db.Payments.SingleAsync(p => p.Id == paymentId, cancellationToken);
            if (payment.IsVoided)
            {
                throw PaymentErrors.AlreadyVoided();
            }

            var allocations = await db.PaymentAllocations.AsNoTracking().Where(a => a.PaymentId == paymentId).ToListAsync(cancellationToken);
            foreach (var allocation in allocations)
            {
                var share = allocation.AllocatedAmount;
                var updated = await db.RentCharges
                    .Where(c => c.Id == allocation.RentChargeId && c.PaidAmount >= share)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.PaidAmount, c => c.PaidAmount - share)
                        .SetProperty(c => c.UpdatedAt, now), cancellationToken);
                if (updated == 0)
                {
                    throw new InvalidOperationException($"Charge {allocation.RentChargeId} has less paid than payment {paymentId} allocated to it.");
                }
            }

            payment.Void(request.Reason, ActorId, now);
            audit.Record(VoidedAction, nameof(Payment), paymentId, new { amount = payment.Amount, reason = payment.VoidReason });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(paymentId, cancellationToken);
    }

    public async Task<PaymentDto> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewTenants);
        return await LoadAsync(paymentId, cancellationToken) ?? throw PaymentErrors.PaymentNotFound();
    }

    public async Task<PagedResult<PaymentSummary>> ListAsync(PaymentListQuery query, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewTenants);
        var payments = db.Payments.AsNoTracking();
        if (query.TenantId is { } tenantId) payments = payments.Where(p => p.TenantId == tenantId);
        if (!query.IncludeVoided) payments = payments.Where(p => p.Status == PaymentStatus.Recorded);
        if (query.PropertyId is { } propertyId)
        {
            payments = payments.Where(p => db.PaymentAllocations.Any(a => a.PaymentId == p.Id
                && db.RentCharges.Any(c => c.Id == a.RentChargeId && c.PropertyId == propertyId)));
        }

        var total = await payments.CountAsync(cancellationToken);
        var page = await (
                from p in payments
                join t in db.Tenants on p.TenantId equals t.Id
                join r in db.Receipts on p.Id equals r.PaymentId
                orderby p.PaymentDate descending, p.CreatedAt descending, p.Id
                select new PaymentSummary(p.Id, p.TenantId, t.FullName, p.PaymentDate, p.Amount, p.Method, p.Status, r.Id, r.ReceiptNumber, p.CreatedAt))
            .Skip(query.Skip).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<PaymentSummary>(page, query.Page, query.PageSize, total);
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private sealed record PayableCharge(Guid Id, decimal Balance, DateOnly PeriodStart, Guid PropertyId, Guid RentAgreementId);

    private async Task<List<PayableCharge>> PayableChargesAsync(Guid tenantId, IReadOnlyList<Guid>? chargeIds, CancellationToken cancellationToken)
    {
        var outstanding = db.RentCharges.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.CancelledAt == null && c.BalanceAmount > 0);
        var selected = chargeIds is { Count: > 0 } ? chargeIds.Distinct().ToList() : null;
        if (selected is not null)
        {
            outstanding = outstanding.Where(c => selected.Contains(c.Id));
        }

        // Oldest dues are settled first.
        var charges = await outstanding
            .OrderBy(c => c.DueDate).ThenBy(c => c.PeriodStart).ThenBy(c => c.Id)
            .Select(c => new PayableCharge(c.Id, c.BalanceAmount, c.PeriodStart, c.PropertyId, c.RentAgreementId))
            .ToListAsync(cancellationToken);

        if (selected is not null && charges.Count != selected.Count)
        {
            throw PaymentErrors.ChargeNotPayable();
        }

        return charges.Count == 0 ? throw PaymentErrors.NothingOutstanding() : charges;
    }

    /// <summary>What the receipt shows, taken from the oldest due being paid (where the tenant stayed then).</summary>
    private async Task<ReceiptDetails> ReceiptDetailsAsync(Guid tenantId, List<PayableCharge> paid, CancellationToken cancellationToken)
    {
        var first = paid[0];
        var organization = await db.Organizations.AsNoTracking().Select(o => o.Name).SingleAsync(cancellationToken);
        var tenant = await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => new { t.FullName, t.Phone }).SingleAsync(cancellationToken);
        var place = await (
                from a in db.RentAgreements
                join p in db.Properties on a.PropertyId equals p.Id
                join r in db.Rooms on a.RoomId equals r.Id
                join b in db.Beds on a.BedId equals b.Id
                where a.Id == first.RentAgreementId
                select new { p.Name, p.Address, p.City, p.State, p.PostalCode, p.ContactPhone, r.RoomNumber, b.Label })
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        var address = string.Join(", ", new[] { place.Address, place.City, place.State, place.PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new ReceiptDetails(
            organization, place.Name, address, place.ContactPhone, tenant.FullName, tenant.Phone, place.RoomNumber, place.Label,
            Receipt.PeriodLabelFor([.. paid.Select(c => c.PeriodStart)]));
    }

    private Task<Payment?> FindByKeyAsync(string key, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.IdempotencyKey == key, cancellationToken);

    private async Task<RecordPaymentResult> ReplayAsync(Payment earlier, RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        EnsureSameRequest(earlier, request);
        return await ResultAsync(earlier.Id, cancellationToken);
    }

    private static void EnsureSameRequest(Payment earlier, RecordPaymentRequest request)
    {
        if (earlier.TenantId != request.TenantId || earlier.Amount != request.Amount)
        {
            throw PaymentErrors.IdempotencyKeyReused();
        }
    }

    private async Task<RecordPaymentResult> ResultAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await LoadAsync(paymentId, cancellationToken) ?? throw PaymentErrors.PaymentNotFound();
        var receipt = await ReceiptService.Project(db.Receipts.AsNoTracking().Where(r => r.Id == payment.ReceiptId), db)
            .SingleAsync(cancellationToken);
        return new RecordPaymentResult(payment, receipt);
    }

    private async Task<PaymentDto?> LoadAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var row = await (
                from p in db.Payments.AsNoTracking()
                join t in db.Tenants on p.TenantId equals t.Id
                join r in db.Receipts on p.Id equals r.PaymentId
                from u in db.Users.Where(u => u.Id == p.RecordedByUserId).DefaultIfEmpty()
                where p.Id == paymentId
                select new { Payment = p, TenantName = t.FullName, ReceiptId = r.Id, r.ReceiptNumber, RecordedBy = u == null ? null : u.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var allocations = await (
                from a in db.PaymentAllocations.AsNoTracking()
                join c in db.RentCharges on a.RentChargeId equals c.Id
                where a.PaymentId == paymentId
                orderby c.DueDate, c.PeriodStart
                select new PaymentAllocationDto(c.Id, c.PeriodStart, c.DueDate, a.AllocatedAmount))
            .ToListAsync(cancellationToken);

        var payment = row.Payment;
        return new PaymentDto(
            payment.Id, payment.TenantId, row.TenantName, payment.PaymentDate, payment.Amount, payment.Method,
            payment.ReferenceNumber, payment.Notes, payment.Status, row.RecordedBy, payment.CreatedAt, payment.VoidedAt,
            payment.VoidReason, row.ReceiptId, row.ReceiptNumber, allocations);
    }

    private Guid? ActorId => currentUser.IsAuthenticated && currentUser.UserId != Guid.Empty ? currentUser.UserId : null;

    private static string TenantLock(Guid tenantId) => string.Create(CultureInfo.InvariantCulture, $"tenant-payments:{tenantId}");

    private static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        key = key.Trim();
        return key.Length > Payment.IdempotencyKeyMaxLength ? throw PaymentErrors.IdempotencyKeyInvalid() : key;
    }
}

internal static class PaymentErrors
{
    public static NotFoundException PaymentNotFound() => new("PAYMENT_NOT_FOUND", "The requested payment was not found.");

    public static NotFoundException TenantNotFound() => new("TENANT_NOT_FOUND", "The requested tenant was not found.");

    public static NotFoundException ReceiptNotFound() => new("RECEIPT_NOT_FOUND", "The requested receipt was not found.");

    public static ConflictException NothingOutstanding() => new("NOTHING_OUTSTANDING", "This tenant has no dues to pay right now.");

    public static ConflictException AlreadyVoided() => new("PAYMENT_ALREADY_VOIDED", "This payment has already been voided.");

    public static ConflictException DuesChanged() =>
        new("DUES_CHANGED", "This tenant's dues changed while saving. Check the amount and try again.");

    public static ConflictException IdempotencyKeyReused() =>
        new("IDEMPOTENCY_KEY_REUSED", "This request key was already used for a different payment.");

    public static ValidationException IdempotencyKeyInvalid() =>
        new("IDEMPOTENCY_KEY_INVALID", $"The Idempotency-Key header can be at most {Payment.IdempotencyKeyMaxLength} characters.");

    public static ValidationException ExceedsOutstanding(decimal owed)
    {
        var message = string.Create(CultureInfo.InvariantCulture, $"The amount is more than the ₹{owed:#,##0.##} this tenant owes.");
        return new("PAYMENT_EXCEEDS_OUTSTANDING", message, new Dictionary<string, string[]> { ["amount"] = [message] });
    }

    public static ValidationException ChargeNotPayable() =>
        new("CHARGE_NOT_PAYABLE", "One or more of the selected dues is already settled or is not this tenant's.",
            new Dictionary<string, string[]> { ["chargeIds"] = ["Choose dues this tenant still owes."] });

    public static ValidationException DateInFuture() =>
        new("DATE_IN_FUTURE", "The payment date cannot be in the future.",
            new Dictionary<string, string[]> { ["paymentDate"] = ["The payment date cannot be in the future."] });
}

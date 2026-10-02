using RentApp.Domain.Common;

namespace RentApp.Domain.Payments;

public enum PaymentMethod
{
    Cash = 1,
    Upi = 2,
    BankTransfer = 3,
    Card = 4,
    Other = 5,
}

public enum PaymentStatus
{
    Recorded = 1,

    /// <summary>Reversed by the owner: its allocations no longer count. The record is kept forever.</summary>
    Voided = 2,
}

/// <summary>
/// Money received from a tenant. Financial record: never deleted or edited; mistakes are corrected by
/// voiding (with a reason and the acting user). Its amount is split across rent charges by allocations.
/// </summary>
public sealed class Payment : Entity, IOrganizationScoped
{
    public const int ReferenceMaxLength = 100;
    public const int NotesMaxLength = 500;
    public const int VoidReasonMaxLength = 200;
    public const int IdempotencyKeyMaxLength = 64;

    private Payment()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid TenantId { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string? ReferenceNumber { get; private set; }

    public string? Notes { get; private set; }

    public PaymentStatus Status { get; private set; }

    public Guid? RecordedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Client-supplied key so a retried request (flaky network) never records the payment twice.</summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset? VoidedAt { get; private set; }

    public Guid? VoidedByUserId { get; private set; }

    public string? VoidReason { get; private set; }

    public bool IsVoided => Status == PaymentStatus.Voided;

    public static Payment Record(
        Guid organizationId, Guid tenantId, DateOnly paymentDate, decimal amount, PaymentMethod method,
        string? referenceNumber, string? notes, Guid? recordedByUserId, string? idempotencyKey, DateTimeOffset at) => new()
    {
        OrganizationId = organizationId,
        TenantId = tenantId,
        PaymentDate = paymentDate,
        Amount = Money.EnsurePositive(amount, nameof(amount)),
        Method = method,
        ReferenceNumber = Text.NullIfBlank(referenceNumber),
        Notes = Text.NullIfBlank(notes),
        Status = PaymentStatus.Recorded,
        RecordedByUserId = recordedByUserId,
        IdempotencyKey = Text.NullIfBlank(idempotencyKey),
        CreatedAt = at,
    };

    public void Void(string reason, Guid? byUserId, DateTimeOffset at)
    {
        if (IsVoided)
        {
            throw new InvalidOperationException("The payment is already voided.");
        }

        Status = PaymentStatus.Voided;
        VoidReason = reason.Trim();
        VoidedByUserId = byUserId;
        VoidedAt = at;
    }
}

/// <summary>The part of a payment applied to one rent charge. Immutable.</summary>
public sealed class PaymentAllocation : Entity, IOrganizationScoped
{
    private PaymentAllocation()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid PaymentId { get; private set; }

    public Guid RentChargeId { get; private set; }

    public decimal AllocatedAmount { get; private set; }

    public static PaymentAllocation Create(Guid organizationId, Guid paymentId, Guid rentChargeId, decimal amount) => new()
    {
        OrganizationId = organizationId,
        PaymentId = paymentId,
        RentChargeId = rentChargeId,
        AllocatedAmount = Money.EnsurePositive(amount, nameof(amount)),
    };
}

/// <summary>
/// Split a payment across outstanding charges in the given order (oldest first by default), never
/// putting more on a charge than its balance. Pure function, unit-tested.
/// </summary>
public static class PaymentAllocator
{
    public sealed record Outstanding(Guid ChargeId, decimal Balance);

    public sealed record Allocation(Guid ChargeId, decimal Amount);

    public static IReadOnlyList<Allocation> Allocate(decimal amount, IEnumerable<Outstanding> chargesInOrder)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive.");
        }

        var remaining = amount;
        var result = new List<Allocation>();
        foreach (var charge in chargesInOrder)
        {
            if (remaining == 0)
            {
                break;
            }

            if (charge.Balance <= 0)
            {
                continue;
            }

            var share = Math.Min(remaining, charge.Balance);
            result.Add(new Allocation(charge.ChargeId, share));
            remaining -= share;
        }

        if (remaining > 0)
        {
            throw new InvalidOperationException("The amount is more than the outstanding balance.");
        }

        return result;
    }
}

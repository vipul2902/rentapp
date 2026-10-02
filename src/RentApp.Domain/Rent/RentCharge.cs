using RentApp.Domain.Common;

namespace RentApp.Domain.Rent;

/// <summary>
/// Rent owed for one month of one tenancy. Financial record: never deleted. Consistency strategy:
/// <see cref="Amount"/> is fixed at creation; <see cref="PaidAmount"/> and <see cref="AdjustedAmount"/>
/// only change through atomic conditional SQL updates (so they can never exceed the amount, even under
/// concurrency); the balance is a database-generated column, and a check constraint enforces
/// paid + adjusted &lt;= amount.
/// </summary>
public sealed class RentCharge : Entity, IAuditableEntity, IOrganizationScoped
{
    private RentCharge()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid RentAgreementId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PropertyId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public DateOnly DueDate { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Sum of payment allocations (Phase 6).</summary>
    public decimal PaidAmount { get; private set; }

    /// <summary>Sum of waivers/adjustments.</summary>
    public decimal AdjustedAmount { get; private set; }

    /// <summary>Amount − Paid − Adjusted; computed by the database.</summary>
    public decimal BalanceAmount { get; private set; }

    /// <summary>Set when the charge is no longer owed (move-out/cancellation before payment).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsCancelled => CancelledAt is not null;

    public static RentCharge Create(
        Guid organizationId, Guid rentAgreementId, Guid tenantId, Guid propertyId, ScheduledCharge scheduled) => new()
    {
        OrganizationId = organizationId,
        RentAgreementId = rentAgreementId,
        TenantId = tenantId,
        PropertyId = propertyId,
        PeriodStart = scheduled.PeriodStart,
        PeriodEnd = scheduled.PeriodEnd,
        DueDate = scheduled.DueDate,
        Amount = Money.EnsurePositive(scheduled.Amount, nameof(scheduled)),
        BalanceAmount = scheduled.Amount,
    };
}

/// <summary>An audited waiver/discount that reduces a charge's balance. Immutable.</summary>
public sealed class RentChargeAdjustment : Entity, IOrganizationScoped
{
    public const int ReasonMaxLength = 200;

    private RentChargeAdjustment()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid RentChargeId { get; private set; }

    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static RentChargeAdjustment Create(
        Guid organizationId, Guid rentChargeId, decimal amount, string reason, Guid? createdByUserId, DateTimeOffset at) => new()
    {
        OrganizationId = organizationId,
        RentChargeId = rentChargeId,
        Amount = Money.EnsurePositive(amount, nameof(amount)),
        Reason = reason.Trim(),
        CreatedByUserId = createdByUserId,
        CreatedAt = at,
    };
}

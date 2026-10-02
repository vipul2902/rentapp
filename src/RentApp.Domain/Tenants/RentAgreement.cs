using RentApp.Domain.Common;

namespace RentApp.Domain.Tenants;

/// <summary>
/// A tenancy: one tenant in one bed for a period, with the agreed rent, deposit and due day. Moving a
/// tenant ends the agreement and starts a new one, so the history of who lived where is never lost.
/// Dates are calendar dates in the organization's time zone; <see cref="EndDate"/> is the last day
/// in the bed (inclusive).
/// </summary>
public sealed class RentAgreement : Entity, IAuditableEntity, IOrganizationScoped
{
    public const int MinDueDay = 1;
    public const int MaxDueDay = 31;

    private RentAgreement()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid RoomId { get; private set; }

    public Guid BedId { get; private set; }

    public decimal MonthlyRent { get; private set; }

    public decimal SecurityDeposit { get; private set; }

    /// <summary>Day of month rent is due. Months without that day use their last day (Phase 5).</summary>
    public int RentDueDay { get; private set; }

    /// <summary>Move-in date.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Last day in the bed; set when the agreement ends.</summary>
    public DateOnly? EndDate { get; private set; }

    public AgreementStatus Status { get; private set; }

    public AgreementEndReason? EndReason { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsActive => Status == AgreementStatus.Active;

    public static RentAgreement Start(
        Guid organizationId, Guid tenantId, Guid propertyId, Guid roomId, Guid bedId,
        decimal monthlyRent, decimal securityDeposit, int rentDueDay, DateOnly startDate)
    {
        var agreement = new RentAgreement
        {
            OrganizationId = organizationId,
            TenantId = tenantId,
            PropertyId = propertyId,
            RoomId = roomId,
            BedId = bedId,
            StartDate = startDate,
            Status = AgreementStatus.Active,
        };
        agreement.UpdateTerms(monthlyRent, securityDeposit, rentDueDay);
        return agreement;
    }

    public void UpdateTerms(decimal monthlyRent, decimal securityDeposit, int rentDueDay)
    {
        if (rentDueDay is < MinDueDay or > MaxDueDay)
        {
            throw new ArgumentOutOfRangeException(nameof(rentDueDay), rentDueDay, "Due day must be 1-31.");
        }

        MonthlyRent = Money.EnsurePositive(monthlyRent, nameof(monthlyRent));
        SecurityDeposit = Money.EnsureNonNegative(securityDeposit, nameof(securityDeposit));
        RentDueDay = rentDueDay;
    }

    /// <summary>Cancels a tenancy whose move-in date has not arrived yet.</summary>
    public void Cancel(DateOnly today)
    {
        if (StateOn(today) != TenancyState.Upcoming)
        {
            throw new InvalidOperationException("Only a tenancy that has not started yet can be cancelled.");
        }

        End(StartDate, AgreementEndReason.Cancelled);
    }

    public void End(DateOnly lastDay, AgreementEndReason reason)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("The agreement has already ended.");
        }

        if (lastDay < StartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(lastDay), lastDay, "The last day cannot be before the move-in date.");
        }

        EndDate = lastDay;
        EndReason = reason;
        Status = AgreementStatus.Ended;
    }

    /// <summary>Upcoming until the move-in date arrives, then current; ended once ended.</summary>
    public TenancyState StateOn(DateOnly today) =>
        !IsActive ? TenancyState.Ended : StartDate > today ? TenancyState.Upcoming : TenancyState.Current;
}

public enum AgreementStatus
{
    Active = 1,
    Ended = 2,
}

public enum AgreementEndReason
{
    MovedOut = 1,

    /// <summary>The tenant moved to another bed; a new agreement follows.</summary>
    Transferred = 2,

    /// <summary>A booking ended before the move-in date; the tenant never stayed, so nothing is ever charged.</summary>
    Cancelled = 3,
}

/// <summary>How a tenancy relates to today. A bed with an upcoming tenancy shows as reserved.</summary>
public enum TenancyState
{
    None = 0,
    Upcoming = 1,
    Current = 2,
    Ended = 3,
}

using System.ComponentModel.DataAnnotations;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Validation;
using RentApp.Domain.Tenants;

namespace RentApp.Application.Tenants;

// ---- Requests -------------------------------------------------------------------------------------

/// <summary>Personal details. Deliberately no identity-document fields (spec §2, §15).</summary>
public class TenantDetailsRequest
{
    [Required(ErrorMessage = "Enter the tenant's name.")]
    [StringLength(Tenant.NameMaxLength, MinimumLength = 2, ErrorMessage = "Name must be 2 to 120 characters.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter the tenant's phone number.")]
    [RegularExpression(FieldRules.PhonePattern, ErrorMessage = FieldRules.PhoneMessage)]
    public string Phone { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = FieldRules.EmailMessage)]
    [StringLength(Tenant.EmailMaxLength, ErrorMessage = FieldRules.EmailMessage)]
    public string? Email { get; init; }

    [StringLength(Tenant.NameMaxLength, ErrorMessage = "Name can be at most 120 characters.")]
    public string? EmergencyContactName { get; init; }

    [RegularExpression(FieldRules.PhonePattern, ErrorMessage = FieldRules.PhoneMessage)]
    public string? EmergencyContactPhone { get; init; }

    [StringLength(Tenant.AddressMaxLength, ErrorMessage = "Address can be at most 500 characters.")]
    public string? PermanentAddress { get; init; }
}

public sealed class CreateTenantRequest : TenantDetailsRequest
{
    /// <summary>Optionally assign a bed in the same step.</summary>
    public MoveInRequest? MoveIn { get; init; }
}

public sealed class MoveInRequest
{
    [Required(ErrorMessage = "Choose a bed.")]
    public Guid? BedId { get; init; }

    [Required(ErrorMessage = "Enter the move-in date.")]
    public DateOnly? StartDate { get; init; }

    /// <summary>Defaults to the bed's suggested rent.</summary>
    [Money]
    public decimal? MonthlyRent { get; init; }

    [Money(AllowZero = true)]
    public decimal SecurityDeposit { get; init; }

    [Range(RentAgreement.MinDueDay, RentAgreement.MaxDueDay, ErrorMessage = "Due day must be between 1 and 31.")]
    public int RentDueDay { get; init; } = 5;
}

public sealed class MoveOutRequest
{
    /// <summary>The tenant's last day in the bed. Cannot be in the future. Ignored for bookings that have not started (they are cancelled).</summary>
    [Required(ErrorMessage = "Enter the move-out date.")]
    public DateOnly? MoveOutDate { get; init; }
}

public sealed class MoveTenantRequest
{
    [Required(ErrorMessage = "Choose the new bed.")]
    public Guid? BedId { get; init; }

    /// <summary>First day in the new bed. Cannot be in the future.</summary>
    [Required(ErrorMessage = "Enter the date of the move.")]
    public DateOnly? MoveDate { get; init; }

    /// <summary>Defaults to the new bed's suggested rent, else the current rent.</summary>
    [Money]
    public decimal? MonthlyRent { get; init; }
}

public sealed class UpdateTenancyTermsRequest
{
    [Required(ErrorMessage = "Enter the monthly rent.")]
    [Money]
    public decimal? MonthlyRent { get; init; }

    [Money(AllowZero = true)]
    public decimal SecurityDeposit { get; init; }

    [Range(RentAgreement.MinDueDay, RentAgreement.MaxDueDay, ErrorMessage = "Due day must be between 1 and 31.")]
    public int RentDueDay { get; init; } = 5;
}

public enum TenantFilter
{
    /// <summary>Everyone not archived.</summary>
    All = 0,

    /// <summary>Living in a bed now, or booked to move in.</summary>
    Current = 1,

    /// <summary>Moved out and not currently in a bed.</summary>
    Former = 2,

    /// <summary>Never assigned a bed.</summary>
    Unassigned = 3,
}

public sealed class TenantListQuery : PageQuery
{
    /// <summary>Name, email, phone (any formatting) or current room number.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    public TenantFilter Filter { get; init; } = TenantFilter.All;

    /// <summary>Only tenants currently in this property / room.</summary>
    public Guid? PropertyId { get; init; }

    public Guid? RoomId { get; init; }
}

// ---- Responses ------------------------------------------------------------------------------------

public sealed record TenancyDto(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    Guid RoomId,
    string RoomNumber,
    Guid BedId,
    string BedLabel,
    decimal MonthlyRent,
    decimal SecurityDeposit,
    int RentDueDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    AgreementStatus Status,
    TenancyState State,
    AgreementEndReason? EndReason);

public sealed record TenantSummary(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    TenantStatus Status,
    TenancyDto? CurrentTenancy,
    DateTimeOffset CreatedAt);

public sealed record TenantDetail(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? PermanentAddress,
    TenantStatus Status,
    TenancyDto? CurrentTenancy,
    IReadOnlyList<TenancyDto> History,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static TenantDetail From(Tenant t, IReadOnlyList<TenancyDto> history) => new(
        t.Id, t.FullName, t.Phone, t.Email, t.EmergencyContactName, t.EmergencyContactPhone, t.PermanentAddress, t.Status,
        history.FirstOrDefault(h => h.Status == AgreementStatus.Active), history, t.CreatedAt, t.UpdatedAt);
}

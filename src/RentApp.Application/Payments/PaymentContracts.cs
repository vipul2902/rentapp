using System.ComponentModel.DataAnnotations;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Validation;
using RentApp.Domain.Payments;

namespace RentApp.Application.Payments;

// ---- Requests -------------------------------------------------------------------------------------

public sealed class RecordPaymentRequest
{
    [Required(ErrorMessage = "Choose the tenant who paid.")]
    public Guid? TenantId { get; init; }

    [Required(ErrorMessage = "Enter the amount received.")]
    [Money]
    public decimal? Amount { get; init; }

    [Required(ErrorMessage = "Enter the payment date.")]
    public DateOnly? PaymentDate { get; init; }

    [Required(ErrorMessage = "Choose how the tenant paid.")]
    [EnumDataType(typeof(PaymentMethod), ErrorMessage = "Choose how the tenant paid.")]
    public PaymentMethod? Method { get; init; }

    [StringLength(Payment.ReferenceMaxLength, ErrorMessage = "Reference can be at most 100 characters.")]
    public string? ReferenceNumber { get; init; }

    [StringLength(Payment.NotesMaxLength, ErrorMessage = "Notes can be at most 500 characters.")]
    public string? Notes { get; init; }

    /// <summary>
    /// Optionally pay specific charges (they are still settled oldest first). By default the payment
    /// settles the tenant's oldest outstanding dues first.
    /// </summary>
    [MaxLength(24, ErrorMessage = "Choose at most 24 dues.")]
    public IReadOnlyList<Guid>? ChargeIds { get; init; }
}

public sealed class VoidPaymentRequest
{
    [Required(ErrorMessage = "Enter why this payment is being voided.")]
    [StringLength(Payment.VoidReasonMaxLength, MinimumLength = 3, ErrorMessage = "Reason must be 3 to 200 characters.")]
    public string Reason { get; init; } = string.Empty;
}

public sealed class PaymentListQuery : PageQuery
{
    public Guid? TenantId { get; init; }

    public Guid? PropertyId { get; init; }

    /// <summary>Voided payments are listed too unless this is false.</summary>
    public bool IncludeVoided { get; init; } = true;
}

// ---- Responses ------------------------------------------------------------------------------------

public sealed record PaymentAllocationDto(Guid RentChargeId, DateOnly PeriodStart, DateOnly DueDate, decimal Amount);

public sealed record PaymentDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod Method,
    string? ReferenceNumber,
    string? Notes,
    PaymentStatus Status,
    string? RecordedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    Guid ReceiptId,
    string ReceiptNumber,
    IReadOnlyList<PaymentAllocationDto> Allocations);

/// <summary>A payment in a list (no allocations).</summary>
public sealed record PaymentSummary(
    Guid Id,
    Guid TenantId,
    string TenantName,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod Method,
    PaymentStatus Status,
    Guid ReceiptId,
    string ReceiptNumber,
    DateTimeOffset CreatedAt);

/// <summary>Returned after recording: everything the success screen needs.</summary>
public sealed record RecordPaymentResult(PaymentDto Payment, ReceiptDto Receipt);

public sealed record ReceiptDto(
    Guid Id,
    Guid PaymentId,
    string ReceiptNumber,
    DateTimeOffset GeneratedAt,
    DateOnly IssuedOn,
    string OrganizationName,
    string PropertyName,
    string PropertyAddress,
    string? PropertyContactPhone,
    string TenantName,
    string TenantPhone,
    string RoomNumber,
    string BedLabel,
    string PeriodLabel,
    decimal Amount,
    DateOnly PaymentDate,
    PaymentMethod Method,
    string? ReferenceNumber,
    bool IsVoid);

/// <summary>A charge's payments, shown on the charge screen.</summary>
public sealed record ChargePaymentDto(
    Guid PaymentId,
    DateOnly PaymentDate,
    PaymentMethod Method,
    decimal AllocatedAmount,
    PaymentStatus Status,
    Guid ReceiptId,
    string ReceiptNumber);

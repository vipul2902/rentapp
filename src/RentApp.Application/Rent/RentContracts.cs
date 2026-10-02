using System.ComponentModel.DataAnnotations;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Validation;
using RentApp.Domain.Rent;

namespace RentApp.Application.Rent;

public enum RentFilter
{
    /// <summary>Everything except cancelled charges.</summary>
    All = 0,

    /// <summary>Anything with a balance left.</summary>
    Outstanding = 1,
    Overdue = 2,
    DueToday = 3,
    Upcoming = 4,

    /// <summary>Settled: paid or waived.</summary>
    Paid = 5,
}

public sealed class RentChargeQuery : PageQuery
{
    public RentFilter Filter { get; init; } = RentFilter.All;

    public Guid? PropertyId { get; init; }

    public Guid? TenantId { get; init; }

    /// <summary>Any date in the month to show (e.g. 2026-10-01).</summary>
    public DateOnly? Month { get; init; }

    /// <summary>Tenant name, phone number (3+ digits) or exact room number.</summary>
    [StringLength(100, ErrorMessage = "Search can be at most 100 characters.")]
    public string? Search { get; init; }
}

public sealed class AdjustChargeRequest
{
    [Required(ErrorMessage = "Enter the amount to waive.")]
    [Money]
    public decimal? Amount { get; init; }

    [Required(ErrorMessage = "Enter a reason, e.g. \"Moved in on the 20th\".")]
    [StringLength(RentChargeAdjustment.ReasonMaxLength, MinimumLength = 3, ErrorMessage = "Reason must be 3 to 200 characters.")]
    public string Reason { get; init; } = string.Empty;
}

public sealed record RentChargeDto(
    Guid Id,
    Guid RentAgreementId,
    Guid TenantId,
    string TenantName,
    string TenantPhone,
    Guid PropertyId,
    string PropertyName,
    string RoomNumber,
    string BedLabel,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal AdjustedAmount,
    decimal Balance,
    RentStatus Status,
    int DaysOverdue);

public sealed record RentAdjustmentDto(Guid Id, decimal Amount, string Reason, DateTimeOffset CreatedAt);

public sealed record RentChargeDetail(
    RentChargeDto Charge, IReadOnlyList<RentAdjustmentDto> Adjustments, IReadOnlyList<Payments.ChargePaymentDto> Payments);

public sealed record AmountCount(decimal Amount, int Count);

/// <summary>Headline rent numbers for the organization (or one property), as of today.</summary>
public sealed record RentSummary(
    DateOnly Today,
    AmountCount Outstanding,
    AmountCount Overdue,
    AmountCount DueToday,
    AmountCount DueThisWeek,
    AmountCount BilledThisMonth);

public sealed record GenerateResult(int Created);

using System.Globalization;
using RentApp.Domain.Common;

namespace RentApp.Domain.Payments;

/// <summary>
/// The receipt for one payment. Its details are copied in when the payment is recorded, so a receipt
/// never changes afterwards (renaming a property or tenant does not rewrite history). Numbers are
/// unique and gap-free per organization and year: REC-2026-000123.
/// </summary>
public sealed class Receipt : Entity, IOrganizationScoped
{
    public const int NumberMaxLength = 32;

    private Receipt()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid PaymentId { get; private set; }

    public string ReceiptNumber { get; private set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>The business date (organization time zone) the receipt was issued; its year is in the number.</summary>
    public DateOnly IssuedOn { get; private set; }

    public string OrganizationName { get; private set; } = string.Empty;

    public string PropertyName { get; private set; } = string.Empty;

    public string PropertyAddress { get; private set; } = string.Empty;

    public string? PropertyContactPhone { get; private set; }

    public string TenantName { get; private set; } = string.Empty;

    public string TenantPhone { get; private set; } = string.Empty;

    public string RoomNumber { get; private set; } = string.Empty;

    public string BedLabel { get; private set; } = string.Empty;

    /// <summary>Human-readable rent period(s) covered, e.g. "October 2026" or "August 2026 – October 2026".</summary>
    public string PeriodLabel { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string? ReferenceNumber { get; private set; }

    public static string FormatNumber(int year, long sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"REC-{year}-{sequence:D6}");

    public static string PeriodLabelFor(IReadOnlyCollection<DateOnly> periodStarts)
    {
        if (periodStarts.Count == 0)
        {
            return string.Empty;
        }

        var first = periodStarts.Min();
        var last = periodStarts.Max();
        string Month(DateOnly d) => d.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return first == last ? Month(first) : $"{Month(first)} – {Month(last)}";
    }

    public static Receipt Issue(
        Guid organizationId, Payment payment, long sequence, DateOnly issuedOn, ReceiptDetails details, DateTimeOffset at) => new()
    {
        OrganizationId = organizationId,
        PaymentId = payment.Id,
        ReceiptNumber = FormatNumber(issuedOn.Year, sequence),
        GeneratedAt = at,
        IssuedOn = issuedOn,
        OrganizationName = details.OrganizationName,
        PropertyName = details.PropertyName,
        PropertyAddress = details.PropertyAddress,
        PropertyContactPhone = Text.NullIfBlank(details.PropertyContactPhone),
        TenantName = details.TenantName,
        TenantPhone = details.TenantPhone,
        RoomNumber = details.RoomNumber,
        BedLabel = details.BedLabel,
        PeriodLabel = details.PeriodLabel,
        Amount = payment.Amount,
        PaymentDate = payment.PaymentDate,
        Method = payment.Method,
        ReferenceNumber = payment.ReferenceNumber,
    };
}

public sealed record ReceiptDetails(
    string OrganizationName,
    string PropertyName,
    string PropertyAddress,
    string? PropertyContactPhone,
    string TenantName,
    string TenantPhone,
    string RoomNumber,
    string BedLabel,
    string PeriodLabel);

/// <summary>Per-organization, per-year receipt sequence. Incremented atomically inside the payment transaction.</summary>
public sealed class ReceiptCounter : IOrganizationScoped
{
    private ReceiptCounter()
    {
    }

    public Guid OrganizationId { get; private set; }

    public int Year { get; private set; }

    public long LastNumber { get; private set; }
}

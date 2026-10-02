using RentApp.Domain.Common;

namespace RentApp.Domain.Tenants;

/// <summary>
/// A person renting a bed. Only the contact details the spec needs are stored; no identity documents.
/// Where the tenant lives is not stored here: it comes from their active <see cref="RentAgreement"/>.
/// </summary>
public sealed class Tenant : Entity, IAuditableEntity, IOrganizationScoped
{
    public const int NameMaxLength = 120;
    public const int PhoneMaxLength = 20;
    public const int EmailMaxLength = 256;
    public const int AddressMaxLength = 500;

    private Tenant()
    {
    }

    public Guid OrganizationId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>As entered, for display and calling.</summary>
    public string Phone { get; private set; } = string.Empty;

    /// <summary>Digits only, so "98765 43210" and "+91-9876543210" are found by the same search.</summary>
    public string PhoneDigits { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? EmergencyContactName { get; private set; }

    public string? EmergencyContactPhone { get; private set; }

    public string? PermanentAddress { get; private set; }

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsArchived => Status == TenantStatus.Archived;

    public static Tenant Create(
        Guid organizationId, string fullName, string phone, string? email,
        string? emergencyContactName, string? emergencyContactPhone, string? permanentAddress)
    {
        var tenant = new Tenant { OrganizationId = organizationId, Status = TenantStatus.Active };
        tenant.Update(fullName, phone, email, emergencyContactName, emergencyContactPhone, permanentAddress);
        return tenant;
    }

    public static string DigitsOnly(string value) => new([.. value.Where(char.IsAsciiDigit)]);

    public void Update(
        string fullName, string phone, string? email,
        string? emergencyContactName, string? emergencyContactPhone, string? permanentAddress)
    {
        FullName = fullName.Trim();
        Phone = phone.Trim();
        PhoneDigits = DigitsOnly(phone);
        Email = Text.NullIfBlank(email);
        EmergencyContactName = Text.NullIfBlank(emergencyContactName);
        EmergencyContactPhone = Text.NullIfBlank(emergencyContactPhone);
        PermanentAddress = Text.NullIfBlank(permanentAddress);
    }

    public void Archive() => Status = TenantStatus.Archived;
}

public enum TenantStatus
{
    Active = 1,
    Archived = 2,
}

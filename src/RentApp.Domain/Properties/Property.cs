using RentApp.Domain.Common;

namespace RentApp.Domain.Properties;

/// <summary>A PG or rental building. Archived rather than deleted, so history stays intact.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1716:Identifiers should not match keywords",
    Justification = "'Property' is the domain term from the spec; this assembly is not consumed from Visual Basic.")]
public sealed class Property : Entity, IAuditableEntity, IOrganizationScoped
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 300;
    public const int CityMaxLength = 100;
    public const int StateMaxLength = 100;
    public const int PostalCodeMaxLength = 12;
    public const int PhoneMaxLength = 20;

    private Property()
    {
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string? State { get; private set; }

    public string? PostalCode { get; private set; }

    public string? ContactPhone { get; private set; }

    public PropertyStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsArchived => Status == PropertyStatus.Archived;

    public static Property Create(
        Guid organizationId, string name, string address, string city, string? state, string? postalCode, string? contactPhone)
    {
        var property = new Property { OrganizationId = organizationId, Status = PropertyStatus.Active };
        property.Update(name, address, city, state, postalCode, contactPhone);
        return property;
    }

    public void Update(string name, string address, string city, string? state, string? postalCode, string? contactPhone)
    {
        Name = name.Trim();
        Address = address.Trim();
        City = city.Trim();
        State = Text.NullIfBlank(state);
        PostalCode = Text.NullIfBlank(postalCode);
        ContactPhone = Text.NullIfBlank(contactPhone);
    }

    public void Archive() => Status = PropertyStatus.Archived;

    public void Restore() => Status = PropertyStatus.Active;
}

public enum PropertyStatus
{
    Active = 1,
    Archived = 2,
}

using RentApp.Domain.Common;

namespace RentApp.Domain.Organizations;

/// <summary>A customer account (a PG business). All business data hangs off an organization.</summary>
public sealed class Organization : Entity, IAuditableEntity
{
    public const int NameMaxLength = 200;

    /// <summary>IANA time zone used to decide what "today" means for due dates. V1 targets India.</summary>
    public const string DefaultTimeZone = "Asia/Kolkata";

    private Organization()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Null only between inserting the organization and its owner in the same transaction.</summary>
    public Guid? OwnerUserId { get; private set; }

    public OrganizationStatus Status { get; private set; }

    public string TimeZone { get; private set; } = DefaultTimeZone;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsActive => Status == OrganizationStatus.Active;

    public static Organization Create(string name) => new()
    {
        Name = name.Trim(),
        Status = OrganizationStatus.Active,
        TimeZone = DefaultTimeZone,
    };

    public void AssignOwner(Guid userId)
    {
        if (OwnerUserId is not null)
        {
            throw new InvalidOperationException("The organization already has an owner.");
        }

        OwnerUserId = userId;
    }
}

using RentApp.Domain.Common;

namespace RentApp.Domain.Properties;

/// <summary>
/// A bed — the unit PGs rent out. Only the owner-controlled status is stored; whether it is occupied is
/// derived from active tenancies (see <see cref="Occupancy"/>), never duplicated here.
/// </summary>
public sealed class Bed : Entity, IAuditableEntity, IOrganizationScoped
{
    public const int LabelMaxLength = 20;
    public const decimal MaxMonthlyRent = 9_999_999_999.99m;

    private Bed()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid RoomId { get; private set; }

    /// <summary>Short label within the room, stored upper-case: "A", "B", "WINDOW".</summary>
    public string Label { get; private set; } = string.Empty;

    public BedStatus Status { get; private set; }

    /// <summary>Suggested rent used to pre-fill a new tenancy. The tenancy's own rent is what gets charged.</summary>
    public decimal? DefaultMonthlyRent { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsArchived => Status == BedStatus.Archived;

    public static Bed Create(Guid organizationId, Guid roomId, string label, decimal? defaultMonthlyRent)
    {
        var bed = new Bed { OrganizationId = organizationId, RoomId = roomId };
        bed.Update(label, BedStatus.Available, defaultMonthlyRent);
        return bed;
    }

    public static string NormalizeLabel(string label) => label.Trim().ToUpperInvariant();

    public void Update(string label, BedStatus status, decimal? defaultMonthlyRent)
    {
        if (status == BedStatus.Archived)
        {
            throw new InvalidOperationException("Use Archive() to archive a bed.");
        }

        if (defaultMonthlyRent is <= 0 or > MaxMonthlyRent)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultMonthlyRent), defaultMonthlyRent, "Rent must be positive.");
        }

        if (defaultMonthlyRent is { } rent && decimal.Round(rent, 2) != rent)
        {
            throw new ArgumentException("Rent cannot have more than 2 decimal places.", nameof(defaultMonthlyRent));
        }

        Label = NormalizeLabel(label);
        Status = status;
        DefaultMonthlyRent = defaultMonthlyRent;
    }

    public void Archive() => Status = BedStatus.Archived;
}

public enum BedStatus
{
    /// <summary>Ready to rent; shows as Vacant unless a tenancy is active.</summary>
    Available = 1,

    /// <summary>Held for someone who has not moved in yet.</summary>
    Reserved = 2,

    /// <summary>Not rentable right now (broken, kept for staff, …).</summary>
    Unavailable = 3,

    Archived = 4,
}

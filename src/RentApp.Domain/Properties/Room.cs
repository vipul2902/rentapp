using RentApp.Domain.Common;

namespace RentApp.Domain.Properties;

/// <summary>A room in a property. Capacity is the most active beds it may hold.</summary>
public sealed class Room : Entity, IAuditableEntity, IOrganizationScoped
{
    public const int NumberMaxLength = 50;
    public const int TypeMaxLength = 50;
    public const int MinCapacity = 1;
    public const int MaxCapacity = 50;

    private Room()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    /// <summary>Room number or name as the owner uses it, e.g. "201" or "Ground floor A".</summary>
    public string RoomNumber { get; private set; } = string.Empty;

    /// <summary>Free-text description such as "AC", "Non-AC" or "Deluxe".</summary>
    public string? RoomType { get; private set; }

    public int Capacity { get; private set; }

    public RoomStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsArchived => Status == RoomStatus.Archived;

    public static Room Create(Guid organizationId, Guid propertyId, string roomNumber, string? roomType, int capacity)
    {
        var room = new Room { OrganizationId = organizationId, PropertyId = propertyId, Status = RoomStatus.Active };
        room.Update(roomNumber, roomType, capacity, RoomStatus.Active, activeBedCount: 0);
        return room;
    }

    /// <param name="activeBedCount">Current non-archived beds; capacity may not drop below it.</param>
    public void Update(string roomNumber, string? roomType, int capacity, RoomStatus status, int activeBedCount)
    {
        if (capacity is < MinCapacity or > MaxCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, $"Capacity must be {MinCapacity}-{MaxCapacity}.");
        }

        if (capacity < activeBedCount)
        {
            throw new InvalidOperationException("Capacity cannot be lower than the number of beds in the room.");
        }

        if (status == RoomStatus.Archived)
        {
            throw new InvalidOperationException("Use Archive() to archive a room.");
        }

        RoomNumber = roomNumber.Trim();
        RoomType = Text.NullIfBlank(roomType);
        Capacity = capacity;
        Status = status;
    }

    public bool CanAddBed(int activeBedCount) => !IsArchived && activeBedCount < Capacity;

    public void Archive() => Status = RoomStatus.Archived;
}

public enum RoomStatus
{
    Active = 1,

    /// <summary>Temporarily out of use (renovation, maintenance); all its beds count as unavailable.</summary>
    Unavailable = 2,

    Archived = 3,
}

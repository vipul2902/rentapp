using System.ComponentModel.DataAnnotations;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Validation;
using RentApp.Domain.Properties;

namespace RentApp.Application.Properties;

// ---- Properties -----------------------------------------------------------------------------------

public sealed class PropertyRequest
{
    [Required(ErrorMessage = "Enter the property name.")]
    [StringLength(Property.NameMaxLength, MinimumLength = 2, ErrorMessage = "Name must be 2 to 200 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter the address.")]
    [StringLength(Property.AddressMaxLength, ErrorMessage = "Address can be at most 300 characters.")]
    public string Address { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter the city.")]
    [StringLength(Property.CityMaxLength, ErrorMessage = "City can be at most 100 characters.")]
    public string City { get; init; } = string.Empty;

    [StringLength(Property.StateMaxLength, ErrorMessage = "State can be at most 100 characters.")]
    public string? State { get; init; }

    [RegularExpression(@"^[A-Za-z0-9 -]{3,12}$", ErrorMessage = "Enter a valid postal code.")]
    public string? PostalCode { get; init; }

    [RegularExpression(FieldRules.PhonePattern, ErrorMessage = FieldRules.PhoneMessage)]
    public string? ContactPhone { get; init; }
}

public enum PropertySort
{
    Name = 1,
    City = 2,
    Newest = 3,
}

public sealed class PropertyListQuery : PageQuery
{
    /// <summary>Matches name or city (case-insensitive, contains).</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    public bool IncludeArchived { get; init; }

    public PropertySort Sort { get; init; } = PropertySort.Name;
}

public sealed record PropertyDto(
    Guid Id,
    string Name,
    string Address,
    string City,
    string? State,
    string? PostalCode,
    string? ContactPhone,
    PropertyStatus Status,
    int RoomCount,
    OccupancySummary Occupancy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static PropertyDto From(Property p, int roomCount, OccupancySummary occupancy) => new(
        p.Id, p.Name, p.Address, p.City, p.State, p.PostalCode, p.ContactPhone, p.Status, roomCount, occupancy, p.CreatedAt, p.UpdatedAt);
}

// ---- Rooms ----------------------------------------------------------------------------------------

public sealed class CreateRoomRequest
{
    [Required(ErrorMessage = "Enter the room number or name.")]
    [StringLength(Room.NumberMaxLength, MinimumLength = 1, ErrorMessage = "Room number can be at most 50 characters.")]
    public string RoomNumber { get; init; } = string.Empty;

    [StringLength(Room.TypeMaxLength, ErrorMessage = "Room type can be at most 50 characters.")]
    public string? RoomType { get; init; }

    [Range(Room.MinCapacity, Room.MaxCapacity, ErrorMessage = "Capacity must be between 1 and 50 beds.")]
    public int Capacity { get; init; } = 1;

    /// <summary>Create one bed per capacity slot, labelled A, B, C… (default true).</summary>
    public bool CreateBeds { get; init; } = true;

    /// <summary>Suggested monthly rent applied to the beds created with the room.</summary>
    [Money]
    public decimal? DefaultMonthlyRent { get; init; }
}

public sealed class UpdateRoomRequest
{
    [Required(ErrorMessage = "Enter the room number or name.")]
    [StringLength(Room.NumberMaxLength, MinimumLength = 1, ErrorMessage = "Room number can be at most 50 characters.")]
    public string RoomNumber { get; init; } = string.Empty;

    [StringLength(Room.TypeMaxLength, ErrorMessage = "Room type can be at most 50 characters.")]
    public string? RoomType { get; init; }

    [Range(Room.MinCapacity, Room.MaxCapacity, ErrorMessage = "Capacity must be between 1 and 50 beds.")]
    public int Capacity { get; init; } = 1;

    [AllowedValues(RoomStatus.Active, RoomStatus.Unavailable, ErrorMessage = "Status must be Active or Unavailable.")]
    public RoomStatus Status { get; init; } = RoomStatus.Active;
}

public sealed record RoomDto(
    Guid Id,
    Guid PropertyId,
    string RoomNumber,
    string? RoomType,
    int Capacity,
    RoomStatus Status,
    IReadOnlyList<BedDto> Beds,
    OccupancySummary Occupancy);

// ---- Beds -----------------------------------------------------------------------------------------

public sealed class CreateBedRequest
{
    /// <summary>Optional; defaults to the next free letter (A, B, C…).</summary>
    [StringLength(Bed.LabelMaxLength, ErrorMessage = "Bed label can be at most 20 characters.")]
    public string? Label { get; init; }

    [Money]
    public decimal? DefaultMonthlyRent { get; init; }
}

public sealed class UpdateBedRequest
{
    [Required(ErrorMessage = "Enter the bed label.")]
    [StringLength(Bed.LabelMaxLength, MinimumLength = 1, ErrorMessage = "Bed label can be at most 20 characters.")]
    public string Label { get; init; } = string.Empty;

    [AllowedValues(BedStatus.Available, BedStatus.Reserved, BedStatus.Unavailable, ErrorMessage = "Status must be Available, Reserved or Unavailable.")]
    public BedStatus Status { get; init; } = BedStatus.Available;

    [Money]
    public decimal? DefaultMonthlyRent { get; init; }
}

public sealed record BedDto(
    Guid Id,
    Guid RoomId,
    string Label,
    BedStatus Status,
    BedOccupancy Occupancy,
    decimal? DefaultMonthlyRent)
{
    public static BedDto From(Bed bed, RoomStatus roomStatus, bool hasActiveTenancy) => new(
        bed.Id, bed.RoomId, bed.Label, bed.Status,
        RentApp.Domain.Properties.Occupancy.Derive(bed.Status, roomStatus, hasActiveTenancy), bed.DefaultMonthlyRent);
}

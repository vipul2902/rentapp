using RentApp.Application.Common.Errors;

namespace RentApp.Application.Properties;

internal static class PropertyErrors
{
    public static NotFoundException PropertyNotFound() => new("PROPERTY_NOT_FOUND", "The requested property was not found.");

    public static NotFoundException RoomNotFound() => new("ROOM_NOT_FOUND", "The requested room was not found.");

    public static NotFoundException BedNotFound() => new("BED_NOT_FOUND", "The requested bed was not found.");

    public static ConflictException PropertyArchived() =>
        new("PROPERTY_ARCHIVED", "This property is archived. Restore it before making changes.");

    public static ConflictException RoomArchived() => new("ROOM_ARCHIVED", "This room is archived and can no longer be changed.");

    public static ConflictException BedArchived() => new("BED_ARCHIVED", "This bed is archived and can no longer be changed.");

    public static ConflictException RoomNumberTaken(string roomNumber) =>
        new("ROOM_NUMBER_TAKEN", $"Room {roomNumber} already exists in this property.");

    public static ConflictException BedLabelTaken(string label) =>
        new("BED_LABEL_TAKEN", $"Bed {label} already exists in this room.");

    public static ConflictException RoomFull(string roomNumber, int capacity) =>
        new("ROOM_FULL", $"Room {roomNumber} already has {capacity} of {capacity} beds. Increase its capacity first.");

    public static ValidationException CapacityBelowBeds(int beds) =>
        new("CAPACITY_BELOW_BED_COUNT", $"This room has {beds} beds. Archive beds first, or keep the capacity at {beds} or more.");
}

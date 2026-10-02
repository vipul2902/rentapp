using RentApp.Application.Common.Errors;

namespace RentApp.Application.Tenants;

internal static class TenantErrors
{
    public static NotFoundException TenantNotFound() => new("TENANT_NOT_FOUND", "The requested tenant was not found.");

    public static NotFoundException BedNotFound() => new("BED_NOT_FOUND", "The selected bed was not found.");

    public static ConflictException TenantArchived() => new("TENANT_ARCHIVED", "This tenant is archived and can no longer be changed.");

    public static ConflictException AlreadyAssigned() =>
        new("TENANT_ALREADY_ASSIGNED", "This tenant already has a bed. Use Move tenant to change beds.");

    public static ConflictException HasActiveTenancy() =>
        new("TENANT_HAS_ACTIVE_TENANCY", "This tenant still has a bed. Move them out before archiving.");

    public static ConflictException NoActiveTenancy() => new("NO_ACTIVE_TENANCY", "This tenant does not currently have a bed.");

    public static ConflictException BedOccupied(string label) => new("BED_OCCUPIED", $"Bed {label} already has a tenant. Choose another bed.");

    public static ConflictException BedNotRentable(string label) =>
        new("BED_UNAVAILABLE", $"Bed {label} is marked unavailable. Mark it available first.");

    public static ConflictException RoomNotRentable(string roomNumber) =>
        new("ROOM_UNAVAILABLE", $"Room {roomNumber} is unavailable or archived. Choose another room.");

    public static ConflictException PropertyArchived() =>
        new("PROPERTY_ARCHIVED", "This property is archived. Restore it before adding tenants.");

    public static ValidationException SameBed() => new("SAME_BED", "The tenant is already in this bed.");

    public static ValidationException RentRequired() =>
        new("RENT_REQUIRED", "Enter the monthly rent (this bed has no suggested rent).",
            new Dictionary<string, string[]> { ["monthlyRent"] = ["Enter the monthly rent."] });

    public static ValidationException DateInFuture(string field, string what) =>
        new("DATE_IN_FUTURE", $"The {what} cannot be in the future.",
            new Dictionary<string, string[]> { [field] = [$"The {what} cannot be in the future."] });

    public static ValidationException DateBeforeMoveIn(string field) =>
        new("DATE_BEFORE_MOVE_IN", "This date is before the tenant moved in.",
            new Dictionary<string, string[]> { [field] = ["This date is before the tenant moved in."] });

    public static ValidationException MoveInOutOfRange() =>
        new("MOVE_IN_OUT_OF_RANGE", "The move-in date must be within the last 10 years and at most one year ahead.",
            new Dictionary<string, string[]> { ["startDate"] = ["Choose a date within the last 10 years and at most one year ahead."] });
}

public static class TenantAuditActions
{
    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantArchived = "tenant.archived";
    public const string MovedIn = "tenant.moved_in";
    public const string MovedOut = "tenant.moved_out";
    public const string Moved = "tenant.moved";
    public const string BookingCancelled = "tenancy.cancelled";
    public const string TermsUpdated = "tenancy.terms_updated";
}

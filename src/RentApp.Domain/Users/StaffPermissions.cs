namespace RentApp.Domain.Users;

/// <summary>
/// Configurable staff permissions (spec §3). Owners implicitly hold all of them. Stored as a bit set;
/// new values must use the next unused power of two and never renumber existing ones.
/// </summary>
[Flags]
public enum StaffPermissions
{
    None = 0,
    ViewProperties = 1 << 0,
    ViewTenants = 1 << 1,
    RecordPayments = 1 << 2,
    GenerateReceipts = 1 << 3,
    SendReminders = 1 << 4,

    All = ViewProperties | ViewTenants | RecordPayments | GenerateReceipts | SendReminders,
}

public static class StaffPermissionsExtensions
{
    private static readonly StaffPermissions[] Individual =
    [
        StaffPermissions.ViewProperties,
        StaffPermissions.ViewTenants,
        StaffPermissions.RecordPayments,
        StaffPermissions.GenerateReceipts,
        StaffPermissions.SendReminders,
    ];

    /// <summary>Each individual permission that is set, in a stable order. Never includes None/All.</summary>
    public static IReadOnlyList<StaffPermissions> ToList(this StaffPermissions permissions) =>
        [.. Individual.Where(p => (permissions & p) == p)];

    /// <summary>Combines a list into a bit set, dropping any bits that are not defined permissions.</summary>
    public static StaffPermissions Combine(IEnumerable<StaffPermissions> permissions) =>
        permissions.Aggregate(StaffPermissions.None, (all, p) => all | p) & StaffPermissions.All;
}

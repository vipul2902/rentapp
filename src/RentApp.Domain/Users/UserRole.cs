namespace RentApp.Domain.Users;

public enum UserRole
{
    /// <summary>Full control of the organization, including staff and permissions.</summary>
    Owner = 1,

    /// <summary>Manager/staff member limited to the <see cref="StaffPermissions"/> the owner grants.</summary>
    Staff = 2,
}

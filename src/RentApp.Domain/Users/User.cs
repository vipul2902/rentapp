using RentApp.Domain.Common;

namespace RentApp.Domain.Users;

/// <summary>Someone who signs in to manage an organization: the owner or a staff member.</summary>
public sealed class User : Entity, IAuditableEntity, IOrganizationScoped
{
    public const int NameMaxLength = 120;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 20;

    private User()
    {
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>As entered (trimmed), for display.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Upper-cased email used for login lookup and the global uniqueness constraint.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    /// <summary>Granted permissions for staff. Always None for owners; use <see cref="EffectivePermissions"/>.</summary>
    public StaffPermissions Permissions { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsActive => Status == UserStatus.Active;

    public bool IsOwner => Role == UserRole.Owner;

    public StaffPermissions EffectivePermissions => IsOwner ? StaffPermissions.All : Permissions;

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public static User CreateOwner(Guid organizationId, string name, string email, string? phone, string passwordHash) =>
        Create(organizationId, name, email, phone, passwordHash, UserRole.Owner, StaffPermissions.None);

    public static User CreateStaff(
        Guid organizationId, string name, string email, string? phone, string passwordHash, StaffPermissions permissions) =>
        Create(organizationId, name, email, phone, passwordHash, UserRole.Staff, permissions & StaffPermissions.All);

    public void SetPermissions(StaffPermissions permissions)
    {
        EnsureStaff();
        Permissions = permissions & StaffPermissions.All;
    }

    public void Disable()
    {
        EnsureStaff();
        Status = UserStatus.Disabled;
    }

    public void Enable() => Status = UserStatus.Active;

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    private static User Create(
        Guid organizationId, string name, string email, string? phone, string passwordHash, UserRole role, StaffPermissions permissions) => new()
    {
        OrganizationId = organizationId,
        Name = name.Trim(),
        Email = email.Trim(),
        NormalizedEmail = NormalizeEmail(email),
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
        PasswordHash = passwordHash,
        Role = role,
        Status = UserStatus.Active,
        Permissions = permissions,
    };

    private void EnsureStaff()
    {
        if (IsOwner)
        {
            throw new InvalidOperationException("The owner's role, permissions and status cannot be changed.");
        }
    }
}

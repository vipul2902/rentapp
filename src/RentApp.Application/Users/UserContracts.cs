using System.ComponentModel.DataAnnotations;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Domain.Users;

namespace RentApp.Application.Users;

public sealed class UserListQuery : PageQuery
{
    /// <summary>Matches name or email (case-insensitive, contains).</summary>
    [StringLength(100)]
    public string? Search { get; init; }
}

public sealed class CreateStaffRequest
{
    [Required(ErrorMessage = "Enter the staff member's name.")]
    [StringLength(User.NameMaxLength, MinimumLength = 2, ErrorMessage = "Name must be 2 to 120 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = FieldRules.EmailMessage)]
    [EmailAddress(ErrorMessage = FieldRules.EmailMessage)]
    [StringLength(User.EmailMaxLength, ErrorMessage = FieldRules.EmailMessage)]
    public string Email { get; init; } = string.Empty;

    [RegularExpression(FieldRules.PhonePattern, ErrorMessage = FieldRules.PhoneMessage)]
    public string? Phone { get; init; }

    /// <summary>Initial password the owner shares with the staff member.</summary>
    [Required(ErrorMessage = FieldRules.PasswordMessage)]
    [StringLength(FieldRules.PasswordMaxLength, MinimumLength = FieldRules.PasswordMinLength, ErrorMessage = FieldRules.PasswordMessage)]
    public string Password { get; init; } = string.Empty;

    public IReadOnlyList<StaffPermissions> Permissions { get; init; } = [];
}

public sealed class UpdatePermissionsRequest
{
    [Required]
    public IReadOnlyList<StaffPermissions> Permissions { get; init; } = [];
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = FieldRules.PasswordMessage)]
    [StringLength(FieldRules.PasswordMaxLength, MinimumLength = FieldRules.PasswordMinLength, ErrorMessage = FieldRules.PasswordMessage)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record StaffMember(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    UserRole Role,
    UserStatus Status,
    IReadOnlyList<StaffPermissions> Permissions,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt)
{
    public static StaffMember From(User user) => new(
        user.Id, user.Name, user.Email, user.Phone, user.Role, user.Status,
        user.EffectivePermissions.ToList(), user.LastLoginAt, user.CreatedAt);
}

using System.ComponentModel.DataAnnotations;
using RentApp.Domain.Organizations;
using RentApp.Domain.Users;

namespace RentApp.Application.Auth;

public static class FieldRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const string PhonePattern = @"^\+?[0-9(][0-9 ()-]{6,18}$";
    public const string PasswordMessage = "Password must be 8 to 128 characters.";
    public const string PhoneMessage = "Enter a valid phone number.";
    public const string EmailMessage = "Enter a valid email address.";
}

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Enter your PG or business name.")]
    [StringLength(Organization.NameMaxLength, MinimumLength = 2, ErrorMessage = "Business name must be 2 to 200 characters.")]
    public string OrganizationName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your name.")]
    [StringLength(User.NameMaxLength, MinimumLength = 2, ErrorMessage = "Name must be 2 to 120 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = FieldRules.EmailMessage)]
    [EmailAddress(ErrorMessage = FieldRules.EmailMessage)]
    [StringLength(User.EmailMaxLength, ErrorMessage = FieldRules.EmailMessage)]
    public string Email { get; init; } = string.Empty;

    [RegularExpression(FieldRules.PhonePattern, ErrorMessage = FieldRules.PhoneMessage)]
    public string? Phone { get; init; }

    [Required(ErrorMessage = FieldRules.PasswordMessage)]
    [StringLength(FieldRules.PasswordMaxLength, MinimumLength = FieldRules.PasswordMinLength, ErrorMessage = FieldRules.PasswordMessage)]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = FieldRules.EmailMessage)]
    [StringLength(User.EmailMaxLength, ErrorMessage = FieldRules.EmailMessage)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [StringLength(FieldRules.PasswordMaxLength, ErrorMessage = "Enter your password.")]
    public string Password { get; init; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
    [Required]
    [StringLength(256)]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record OrganizationSummary(Guid Id, string Name, string TimeZone);

public sealed record UserProfile(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    UserRole Role,
    UserStatus Status,
    IReadOnlyList<StaffPermissions> Permissions,
    OrganizationSummary Organization)
{
    public static UserProfile From(User user, Organization organization) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Phone,
        user.Role,
        user.Status,
        user.EffectivePermissions.ToList(),
        new OrganizationSummary(organization.Id, organization.Name, organization.TimeZone));
}

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfile User);

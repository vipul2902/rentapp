using RentApp.Application.Common.Errors;

namespace RentApp.Application.Auth;

internal static class AuthErrors
{
    // One message for unknown email and wrong password, so responses do not reveal which accounts exist.
    public static UnauthorizedException InvalidCredentials() =>
        new("INVALID_CREDENTIALS", "Incorrect email or password.");

    public static UnauthorizedException InvalidRefreshToken() =>
        new("SESSION_EXPIRED", "Your session has ended. Please sign in again.");

    public static UnauthorizedException AccountDisabled() =>
        new("ACCOUNT_DISABLED", "Your account has been disabled. Please contact the property owner.");

    public static ConflictException EmailTaken() =>
        new("EMAIL_ALREADY_REGISTERED", "An account with this email already exists. Please sign in instead.");
}

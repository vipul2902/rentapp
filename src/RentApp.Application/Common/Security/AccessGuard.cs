using RentApp.Application.Common.Errors;
using RentApp.Domain.Users;

namespace RentApp.Application.Common.Security;

/// <summary>
/// Service-level authorization checks. Controllers already apply the same policies; these keep the rules
/// true for any caller of the service (defense in depth).
/// </summary>
public static class AccessGuard
{
    public static void EnsureOwner(this ICurrentUser user, string message = "Only the owner can make this change.")
    {
        if (!user.IsAuthenticated || !user.IsOwner)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, message);
        }
    }

    public static void EnsurePermission(this ICurrentUser user, StaffPermissions permission)
    {
        if (!user.HasPermission(permission))
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "You do not have permission to do this.");
        }
    }

    /// <summary>Passes if the user holds at least one of the permissions in <paramref name="anyOf"/>.</summary>
    public static void EnsureAnyPermission(this ICurrentUser user, StaffPermissions anyOf)
    {
        if (!anyOf.ToList().Any(user.HasPermission))
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "You do not have permission to do this.");
        }
    }
}

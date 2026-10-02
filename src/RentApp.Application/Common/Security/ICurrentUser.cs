using RentApp.Domain.Users;

namespace RentApp.Application.Common.Security;

/// <summary>
/// The authenticated caller, taken from the validated access token. When <see cref="IsAuthenticated"/> is
/// false, ids are <see cref="Guid.Empty"/>, so organization-scoped queries match nothing (fail closed).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    Guid OrganizationId { get; }

    UserRole? Role { get; }

    StaffPermissions Permissions { get; }

    bool IsOwner => Role == UserRole.Owner;

    bool HasPermission(StaffPermissions permission) =>
        IsAuthenticated && (IsOwner || (Permissions & permission) == permission);
}

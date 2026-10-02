using RentApp.Domain.Users;

namespace RentApp.Application.Common.Security;

/// <summary>
/// The identity background jobs act as: scoped to exactly one organization (so all organization filters
/// and the cross-organization write guard still apply), with owner rights, and no user id (audit entries
/// record a null actor, meaning "system").
/// </summary>
public sealed record SystemCurrentUser(Guid OrganizationId) : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid UserId => Guid.Empty;

    public UserRole? Role => UserRole.Owner;

    public StaffPermissions Permissions => StaffPermissions.All;
}

/// <summary>Per-scope override of the current user, set by background jobs before resolving services.</summary>
public sealed class CurrentUserOverride
{
    public ICurrentUser? User { get; set; }
}

using RentApp.Application.Common.Security;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Security;

/// <summary>Used where there is no HTTP caller (design-time tooling, startup migrations).</summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public static readonly AnonymousCurrentUser Instance = new();

    public bool IsAuthenticated => false;

    public Guid UserId => Guid.Empty;

    public Guid OrganizationId => Guid.Empty;

    public UserRole? Role => null;

    public StaffPermissions Permissions => StaffPermissions.None;
}

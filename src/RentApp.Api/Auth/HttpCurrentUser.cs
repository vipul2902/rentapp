using System.Security.Claims;
using RentApp.Application.Common.Security;
using RentApp.Domain.Users;

namespace RentApp.Api.Auth;

/// <summary>Reads the caller from the validated JWT. Malformed claims are treated as anonymous (fail closed).</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private Snapshot? _authenticated;

    public bool IsAuthenticated => Current.IsAuthenticated;

    public Guid UserId => Current.UserId;

    public Guid OrganizationId => Current.OrganizationId;

    public UserRole? Role => Current.Role;

    public StaffPermissions Permissions => Current.Permissions;

    // Cache only once authenticated: anything read before the authentication middleware ran must not stick.
    private Snapshot Current => _authenticated ?? Read(accessor.HttpContext?.User);

    private Snapshot Read(ClaimsPrincipal? principal)
    {
        var snapshot = Snapshot.From(principal);
        if (snapshot.IsAuthenticated)
        {
            _authenticated = snapshot;
        }

        return snapshot;
    }

    internal sealed record Snapshot(bool IsAuthenticated, Guid UserId, Guid OrganizationId, UserRole? Role, StaffPermissions Permissions)
    {
        private static readonly Snapshot Anonymous = new(false, Guid.Empty, Guid.Empty, null, StaffPermissions.None);

        public static Snapshot From(ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true
                || !Guid.TryParse(principal.FindFirstValue(AppClaimTypes.Subject), out var userId)
                || !Guid.TryParse(principal.FindFirstValue(AppClaimTypes.Organization), out var organizationId)
                || !Enum.TryParse<UserRole>(principal.FindFirstValue(AppClaimTypes.Role), ignoreCase: false, out var role)
                || !Enum.IsDefined(role))
            {
                return Anonymous;
            }

            var permissions = StaffPermissionsExtensions.Combine(
                principal.FindAll(AppClaimTypes.Permission)
                    .Select(c => Enum.TryParse<StaffPermissions>(c.Value, ignoreCase: false, out var p) ? p : StaffPermissions.None));

            return new Snapshot(true, userId, organizationId, role, permissions);
        }
    }
}

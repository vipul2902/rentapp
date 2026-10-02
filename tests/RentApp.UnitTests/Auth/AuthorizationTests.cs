using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RentApp.Api.Auth;
using RentApp.Application.Common.Security;
using RentApp.Domain.Users;

namespace RentApp.UnitTests.Auth;

public class AuthorizationTests
{
    private static readonly string UserId = Guid.NewGuid().ToString();
    private static readonly string OrgId = Guid.NewGuid().ToString();

    [Fact]
    public async Task OwnerPassesEveryPermissionRequirement()
    {
        Assert.True(await Authorize(Principal("Owner"), StaffPermissions.RecordPayments));
    }

    [Fact]
    public async Task StaffPassesOnlyForGrantedPermissions()
    {
        var staff = Principal("Staff", "RecordPayments");

        Assert.True(await Authorize(staff, StaffPermissions.RecordPayments));
        Assert.False(await Authorize(staff, StaffPermissions.SendReminders));
    }

    [Fact]
    public async Task ACombinedRequirementMeansAnyOfThePermissions()
    {
        var receipts = StaffPermissions.RecordPayments | StaffPermissions.GenerateReceipts;

        Assert.True(await Authorize(Principal("Staff", "GenerateReceipts"), receipts));
        Assert.True(await Authorize(Principal("Staff", "RecordPayments"), receipts));
        Assert.False(await Authorize(Principal("Staff", "ViewTenants"), receipts));
    }

    [Fact]
    public void SnapshotReadsValidClaims()
    {
        var snapshot = HttpCurrentUser.Snapshot.From(Principal("Staff", "ViewTenants", "RecordPayments"));

        Assert.True(snapshot.IsAuthenticated);
        Assert.Equal(Guid.Parse(UserId), snapshot.UserId);
        Assert.Equal(Guid.Parse(OrgId), snapshot.OrganizationId);
        Assert.Equal(UserRole.Staff, snapshot.Role);
        Assert.Equal(StaffPermissions.ViewTenants | StaffPermissions.RecordPayments, snapshot.Permissions);
    }

    [Theory]
    [InlineData("not-a-guid", "Staff")]
    [InlineData(null, "Staff")]
    [InlineData("org", "Superuser")]
    [InlineData("org", "99")]
    public void SnapshotTreatsMalformedClaimsAsAnonymous(string? org, string role)
    {
        var claims = new List<Claim> { new(AppClaimTypes.Subject, UserId), new(AppClaimTypes.Role, role) };
        if (org is not null)
        {
            claims.Add(new Claim(AppClaimTypes.Organization, org == "org" ? OrgId : org));
        }

        var snapshot = HttpCurrentUser.Snapshot.From(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));

        Assert.False(snapshot.IsAuthenticated);
        Assert.Equal(Guid.Empty, snapshot.OrganizationId);
    }

    [Fact]
    public void UnauthenticatedPrincipalIsAnonymous()
    {
        var snapshot = HttpCurrentUser.Snapshot.From(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(snapshot.IsAuthenticated);
    }

    private static ClaimsPrincipal Principal(string role, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.Subject, UserId),
            new(AppClaimTypes.Organization, OrgId),
            new(AppClaimTypes.Role, role),
        };
        claims.AddRange(permissions.Select(p => new Claim(AppClaimTypes.Permission, p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", AppClaimTypes.Subject, AppClaimTypes.Role));
    }

    private static async Task<bool> Authorize(ClaimsPrincipal user, StaffPermissions permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await new PermissionAuthorizationHandler().HandleAsync(context);
        return context.HasSucceeded;
    }
}

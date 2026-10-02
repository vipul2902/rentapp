using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Application.Audit;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Users;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class StaffManagementTests(ContainersFixture containers) : IAsyncLifetime
{
    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _owner = null!;
    private AuthResponse _ownerAuth = null!;

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.For(containers);
        _anonymous = _factory.CreateClient();
        _ownerAuth = await _anonymous.RegisterOwnerAsync();
        _owner = _factory.CreateClient(_ownerAuth.AccessToken);
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task OwnerCreatesStaffWhoCanSignInWithGrantedPermissions()
    {
        var staff = await _owner.CreateStaffAsync("Ravi", StaffPermissions.RecordPayments, StaffPermissions.ViewTenants);

        var login = await _anonymous.LoginAsync(staff.Email, StaffPassword);
        var auth = await login.ReadAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(UserRole.Staff, auth.User.Role);
        Assert.Equal([StaffPermissions.ViewTenants, StaffPermissions.RecordPayments], auth.User.Permissions);
        Assert.Equal(_ownerAuth.User.Organization.Id, auth.User.Organization.Id);
    }

    [Fact]
    public async Task StaffCannotManageUsers()
    {
        var staff = await _owner.CreateStaffAsync("Meena", StaffPermissions.All);
        var auth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        using var staffClient = _factory.CreateClient(auth.AccessToken);

        var list = await staffClient.GetAsync(new Uri("/api/v1/users", UriKind.Relative));
        var create = await staffClient.PostAsJsonAsync("/api/v1/users", new { name = "Sneaky", email = UniqueEmail("x"), password = StaffPassword });

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal("FORBIDDEN", (await list.ReadErrorAsync()).Code);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task PermissionChangesAreSavedAndAudited()
    {
        var staff = await _owner.CreateStaffAsync("Kiran", StaffPermissions.ViewTenants);

        var response = await _owner.PutAsJsonAsync($"/api/v1/users/{staff.Id}/permissions", new { permissions = new[] { "RecordPayments", "SendReminders" } });
        var updated = await response.ReadAsync<StaffMember>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([StaffPermissions.RecordPayments, StaffPermissions.SendReminders], updated.Permissions);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditLogs.IgnoreQueryFilters()
            .SingleAsync(a => a.EntityId == staff.Id && a.Action == AuditActions.PermissionsChanged);
        Assert.Equal(_ownerAuth.User.Id, entry.ActorUserId);
        Assert.Contains("ViewTenants", entry.Details, StringComparison.Ordinal);
        Assert.Contains("SendReminders", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisablingStaffBlocksLoginAndEndsSessions()
    {
        var staff = await _owner.CreateStaffAsync("Deepa", StaffPermissions.RecordPayments);
        var session = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();

        var disable = await _owner.PostAsync(new Uri($"/api/v1/users/{staff.Id}/disable", UriKind.Relative), null);
        var refresh = await _anonymous.RefreshAsync(session.RefreshToken);
        var login = await _anonymous.LoginAsync(staff.Email, StaffPassword);

        Assert.Equal(UserStatus.Disabled, (await disable.ReadAsync<StaffMember>()).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("ACCOUNT_DISABLED", (await login.ReadErrorAsync()).Code);

        await _owner.PostAsync(new Uri($"/api/v1/users/{staff.Id}/enable", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, (await _anonymous.LoginAsync(staff.Email, StaffPassword)).StatusCode);
    }

    [Fact]
    public async Task OwnerResetsStaffPassword()
    {
        var staff = await _owner.CreateStaffAsync("Arjun");
        var session = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();

        var reset = await _owner.PostAsJsonAsync($"/api/v1/users/{staff.Id}/reset-password", new { newPassword = "Brand-New-456" });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.LoginAsync(staff.Email, StaffPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _anonymous.LoginAsync(staff.Email, "Brand-New-456")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.RefreshAsync(session.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task OwnerAccountCannotBeDisabledOrRestricted()
    {
        var disable = await _owner.PostAsync(new Uri($"/api/v1/users/{_ownerAuth.User.Id}/disable", UriKind.Relative), null);
        var permissions = await _owner.PutAsJsonAsync($"/api/v1/users/{_ownerAuth.User.Id}/permissions", new { permissions = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, disable.StatusCode);
        Assert.Equal("OWNER_CANNOT_BE_MODIFIED", (await disable.ReadErrorAsync()).Code);
        Assert.Equal(HttpStatusCode.BadRequest, permissions.StatusCode);
    }

    [Fact]
    public async Task StaffEmailMustBeUniqueAcrossTheSystem()
    {
        var response = await _owner.PostAsJsonAsync("/api/v1/users", new { name = "Clash", email = _ownerAuth.User.Email, password = StaffPassword });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EMAIL_ALREADY_REGISTERED", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task ListSupportsPagingAndSearch()
    {
        await _owner.CreateStaffAsync("Zara Alpha");
        await _owner.CreateStaffAsync("Zara Beta");
        await _owner.CreateStaffAsync("Yusuf");

        var page = await (await _owner.GetAsync(new Uri("/api/v1/users?page=1&pageSize=2", UriKind.Relative))).ReadAsync<PagedResult<StaffMember>>();
        var search = await (await _owner.GetAsync(new Uri("/api/v1/users?search=zara", UriKind.Relative))).ReadAsync<PagedResult<StaffMember>>();
        var badPage = await _owner.GetAsync(new Uri("/api/v1/users?pageSize=500", UriKind.Relative));

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(4, page.TotalCount); // owner + 3 staff
        Assert.Equal(UserRole.Owner, page.Items[0].Role);
        Assert.Equal(["Zara Alpha", "Zara Beta"], search.Items.Select(u => u.Name));
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Application.Users;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

/// <summary>Organization A must never see or change organization B's data, through any path.</summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class OrganizationIsolationTests(ContainersFixture containers) : IAsyncLifetime
{
    private RentAppFactory _factory = null!;
    private HttpClient _ownerA = null!;
    private HttpClient _ownerB = null!;
    private StaffMember _staffOfA = null!;

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.For(containers);
        using var anonymous = _factory.CreateClient();
        _ownerA = _factory.CreateClient((await anonymous.RegisterOwnerAsync("Org A PG")).AccessToken);
        _ownerB = _factory.CreateClient((await anonymous.RegisterOwnerAsync("Org B PG")).AccessToken);
        _staffOfA = await _ownerA.CreateStaffAsync("A's Staff", StaffPermissions.RecordPayments);
    }

    public async Task DisposeAsync()
    {
        _ownerA.Dispose();
        _ownerB.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ListingOnlyShowsOwnOrganization()
    {
        var a = await (await _ownerA.GetAsync(new Uri("/api/v1/users?pageSize=100", UriKind.Relative))).ReadAsync<PagedResult<StaffMember>>();
        var b = await (await _ownerB.GetAsync(new Uri("/api/v1/users?pageSize=100", UriKind.Relative))).ReadAsync<PagedResult<StaffMember>>();

        Assert.Contains(a.Items, u => u.Id == _staffOfA.Id);
        Assert.DoesNotContain(b.Items, u => u.Id == _staffOfA.Id);
        Assert.Single(b.Items); // only B's owner
    }

    [Fact]
    public async Task OtherOrganizationsRecordsAreNotFoundForEveryOperation()
    {
        var id = _staffOfA.Id;
        var responses = new[]
        {
            await _ownerB.GetAsync(new Uri($"/api/v1/users/{id}", UriKind.Relative)),
            await _ownerB.PutAsJsonAsync($"/api/v1/users/{id}/permissions", new { permissions = new[] { "SendReminders" } }),
            await _ownerB.PostAsync(new Uri($"/api/v1/users/{id}/disable", UriKind.Relative), null),
            await _ownerB.PostAsync(new Uri($"/api/v1/users/{id}/enable", UriKind.Relative), null),
            await _ownerB.PostAsJsonAsync($"/api/v1/users/{id}/reset-password", new { newPassword = "Hijacked-123" }),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.All(await Task.WhenAll(responses.Select(r => r.ReadErrorAsync())), e => Assert.Equal("USER_NOT_FOUND", e.Code));

        // And nothing changed for A.
        var unchanged = await (await _ownerA.GetAsync(new Uri($"/api/v1/users/{id}", UriKind.Relative))).ReadAsync<StaffMember>();
        Assert.Equal(UserStatus.Active, unchanged.Status);
        Assert.Equal([StaffPermissions.RecordPayments], unchanged.Permissions);
    }

    [Fact]
    public async Task QueriesWithoutAnAuthenticatedCallerReturnNothing()
    {
        await using var db = NewContext(new FakeCurrentUser(authenticated: false, Guid.Empty));

        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Organizations.CountAsync());
        Assert.True(await db.Users.IgnoreQueryFilters().AnyAsync());
    }

    [Fact]
    public async Task CrossOrganizationWritesAreBlockedOnSave()
    {
        await using var db = NewContext(new FakeCurrentUser(authenticated: true, organizationId: Guid.NewGuid()));
        db.Users.Add(User.CreateStaff(Guid.NewGuid(), "Intruder", UniqueEmail("intruder"), null, "hash", StaffPermissions.None));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("cross-organization", error.Message, StringComparison.Ordinal);
    }

    private AppDbContext NewContext(ICurrentUser user)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(containers.Postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OrganizationIsolationInterceptor(user))
            .Options;
        return new AppDbContext(options, user);
    }

    private sealed class FakeCurrentUser(bool authenticated, Guid organizationId) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;

        public Guid UserId => authenticated ? Guid.NewGuid() : Guid.Empty;

        public Guid OrganizationId => organizationId;

        public UserRole? Role => authenticated ? UserRole.Owner : null;

        public StaffPermissions Permissions => StaffPermissions.None;
    }
}

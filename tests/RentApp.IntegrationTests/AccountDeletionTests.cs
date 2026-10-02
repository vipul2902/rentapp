using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Application.Auth;
using RentApp.Application.Payments;
using RentApp.Domain.Organizations;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

/// <summary>In-app account deletion (an App Store requirement): staff accounts and whole organizations.</summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class AccountDeletionTests(ContainersFixture containers) : IAsyncLifetime
{
    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;

    public Task InitializeAsync()
    {
        _factory = RentAppFactory.At(containers, new DateTimeOffset(2026, 10, 12, 6, 30, 0, TimeSpan.Zero));
        _anonymous = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AStaffMemberCanDeleteTheirAccountAndTheirHistoryShowsDeletedUser()
    {
        var ownerAuth = await _anonymous.RegisterOwnerAsync();
        using var owner = _factory.CreateClient(ownerAuth.AccessToken);
        var staff = await owner.CreateStaffAsync("Ravi Staff", StaffPermissions.ViewTenants, StaffPermissions.RecordPayments);
        var staffAuth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        using var staffClient = _factory.CreateClient(staffAuth.AccessToken);

        var property = await owner.CreatePropertyAsync();
        var room = await owner.CreateRoomAsync(property.Id, "1", capacity: 1, rent: 5000m);
        var tenant = await owner.CreateTenantAsync("Rahul", moveIn: new { bedId = room.Beds[0].Id, startDate = "2026-10-01", rentDueDay = 5 });
        var payment = await (await staffClient.RecordPaymentAsync(tenant.Id, 5000m)).ReadAsync<RecordPaymentResult>();

        var wrong = await staffClient.PostAsJsonAsync("/api/v1/auth/delete-account", new { password = "not-it" });
        Assert.Equal((HttpStatusCode.BadRequest, "PASSWORD_INCORRECT"), (wrong.StatusCode, (await wrong.ReadErrorAsync()).Code));

        var deleted = await staffClient.PostAsJsonAsync("/api/v1/auth/delete-account", new { password = StaffPassword });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.LoginAsync(staff.Email, StaffPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.RefreshAsync(staffAuth.RefreshToken)).StatusCode);
        var detail = await (await owner.GetAsync(new Uri($"/api/v1/payments/{payment.Payment.Id}", UriKind.Relative))).ReadAsync<PaymentDto>();
        Assert.Equal("Deleted user", detail.RecordedByName);
        var team = await (await owner.GetAsync(new Uri("/api/v1/users", UriKind.Relative))).Content.ReadAsStringAsync();
        Assert.DoesNotContain(staff.Email, team, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deleted user", team, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync(new Uri($"/api/v1/users/{staff.Id}/enable", UriKind.Relative), null)).StatusCode);

        // The email is free again, and nothing personal is left on the old row.
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync("/api/v1/users", new { name = "Ravi Again", email = staff.Email, password = StaffPassword, permissions = Array.Empty<string>() })).StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters().SingleAsync(u => u.Id == staff.Id);
        Assert.Equal((UserStatus.Deleted, "Deleted user", (string?)null, string.Empty), (row.Status, row.Name, row.Phone, row.PasswordHash));
        Assert.EndsWith("@deleted.invalid", row.Email, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOwnerDeletingTheirAccountClosesTheOrganizationForEveryone()
    {
        var ownerAuth = await _anonymous.RegisterOwnerAsync();
        using var owner = _factory.CreateClient(ownerAuth.AccessToken);
        var staff = await owner.CreateStaffAsync("Meena", StaffPermissions.ViewTenants);
        var staffAuth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        var tenant = await owner.CreateTenantAsync("Kept Record");

        var response = await owner.PostAsJsonAsync("/api/v1/auth/delete-account", new { password = OwnerPassword });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.LoginAsync(ownerAuth.User.Email, OwnerPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.LoginAsync(staff.Email, StaffPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.RefreshAsync(ownerAuth.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.RefreshAsync(staffAuth.RefreshToken)).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var organization = await db.Organizations.IgnoreQueryFilters().SingleAsync(o => o.Id == ownerAuth.User.Organization.Id);
        Assert.Equal(OrganizationStatus.Closed, organization.Status);
        Assert.All(await db.Users.IgnoreQueryFilters().Where(u => u.OrganizationId == organization.Id).ToListAsync(), u => Assert.Equal(UserStatus.Deleted, u.Status));
        // Business records are kept (financial history is permanent), just unreachable.
        Assert.True(await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenant.Id));

        // The same email can start over with a brand-new, empty organization.
        var again = await _anonymous.PostAsJsonAsync("/api/v1/auth/register", new { organizationName = "Fresh PG", name = "Asha", email = ownerAuth.User.Email, password = OwnerPassword });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        using var fresh = _factory.CreateClient((await again.ReadAsync<AuthResponse>()).AccessToken);
        Assert.Equal(0, (await fresh.ListTenantsAsync("filter=All")).TotalCount);
    }
}

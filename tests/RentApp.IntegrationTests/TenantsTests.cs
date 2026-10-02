using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Properties;
using RentApp.Application.Tenants;
using RentApp.Domain.Common;
using RentApp.Domain.Properties;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class TenantsTests(ContainersFixture containers) : IAsyncLifetime
{
    // Organizations default to Asia/Kolkata; "today" must match the API's notion of today.
    private static readonly DateOnly Today = OrganizationCalendar.Today(DateTimeOffset.UtcNow, "Asia/Kolkata");

    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _owner = null!;
    private PropertyDto _property = null!;
    private RoomDto _room = null!;

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.For(containers);
        _anonymous = _factory.CreateClient();
        _owner = _factory.CreateClient((await _anonymous.RegisterOwnerAsync()).AccessToken);
        _property = await _owner.CreatePropertyAsync("Sunrise PG");
        _room = await _owner.CreateRoomAsync(_property.Id, "201", capacity: 3, rent: 8500m);
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    private Guid BedA => _room.Beds[0].Id;

    private Guid BedB => _room.Beds[1].Id;

    // ---- Move-in ---------------------------------------------------------------------------------

    [Fact]
    public async Task AddingATenantWithABedOccupiesIt()
    {
        var tenant = await _owner.CreateTenantAsync("Rahul Sharma", moveIn: new { bedId = BedA, startDate = Today.AddDays(-30), securityDeposit = 10000 });

        var tenancy = tenant.CurrentTenancy!;
        Assert.Equal(TenancyState.Current, tenancy.State);
        Assert.Equal(("Sunrise PG", "201", "A"), (tenancy.PropertyName, tenancy.RoomNumber, tenancy.BedLabel));
        Assert.Equal(8500m, tenancy.MonthlyRent); // from the bed's suggested rent
        Assert.Equal(10000m, tenancy.SecurityDeposit);
        Assert.Equal(5, tenancy.RentDueDay);

        var room = await _owner.GetRoomAsync(_room.Id);
        var bed = room.Beds.Single(b => b.Id == BedA);
        Assert.Equal(BedOccupancy.Occupied, bed.Occupancy);
        Assert.Equal("Rahul Sharma", bed.Tenant!.FullName);

        var property = await (await _owner.GetAsync(new Uri($"/api/v1/properties/{_property.Id}", UriKind.Relative))).ReadAsync<PropertyDto>();
        Assert.Equal(new OccupancySummary(TotalBeds: 3, Occupied: 1, Vacant: 2, Reserved: 0, Unavailable: 0), property.Occupancy);
    }

    [Fact]
    public async Task FutureMoveInHoldsTheBedAndCanBeCancelled()
    {
        var tenant = await _owner.CreateTenantAsync("Amit", moveIn: new { bedId = BedA, startDate = Today.AddDays(10) });

        Assert.Equal(TenancyState.Upcoming, tenant.CurrentTenancy!.State);
        Assert.Equal(BedOccupancy.Reserved, (await _owner.GetRoomAsync(_room.Id)).Beds[0].Occupancy);

        var cancelled = await _owner.MoveOutAsync(tenant.Id, Today);
        Assert.Null(cancelled.CurrentTenancy);
        Assert.Equal(AgreementEndReason.Cancelled, Assert.Single(cancelled.History).EndReason);
        Assert.Equal(BedOccupancy.Vacant, (await _owner.GetRoomAsync(_room.Id)).Beds[0].Occupancy);
    }

    [Fact]
    public async Task ABedCannotBeDoubleBooked()
    {
        await _owner.CreateTenantAsync("First", moveIn: new { bedId = BedA, startDate = Today });
        var second = await _owner.CreateTenantAsync("Second");

        var response = await _owner.PostAsJsonAsync($"/api/v1/tenants/{second.Id}/move-in", new { bedId = BedA, startDate = Today });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("BED_OCCUPIED", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task ConcurrentMoveInsToOneBedSucceedOnlyOnce()
    {
        var tenants = new List<TenantDetail>();
        for (var i = 0; i < 5; i++)
        {
            tenants.Add(await _owner.CreateTenantAsync($"Racer {i}"));
        }

        var results = await Task.WhenAll(tenants.Select(t =>
            _owner.PostAsJsonAsync($"/api/v1/tenants/{t.Id}/move-in", new { bedId = BedA, startDate = Today })));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.RentAgreements.IgnoreQueryFilters().CountAsync(a => a.BedId == BedA && a.Status == AgreementStatus.Active));
    }

    [Fact]
    public async Task ATenantCannotHoldTwoBeds()
    {
        var tenant = await _owner.CreateTenantAsync("Rahul", moveIn: new { bedId = BedA, startDate = Today });

        var response = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-in", new { bedId = BedB, startDate = Today });

        Assert.Equal("TENANT_ALREADY_ASSIGNED", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task RentIsRequiredWhenTheBedHasNoSuggestedRent()
    {
        var bare = await _owner.CreateRoomAsync(_property.Id, "301", capacity: 1);
        var tenant = await _owner.CreateTenantAsync("Kiran");

        var missing = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-in", new { bedId = bare.Beds[0].Id, startDate = Today });
        var given = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-in", new { bedId = bare.Beds[0].Id, startDate = Today, monthlyRent = 7000.50 });

        Assert.Equal("RENT_REQUIRED", (await missing.ReadErrorAsync()).Code);
        Assert.Equal(7000.50m, (await given.ReadAsync<TenantDetail>()).CurrentTenancy!.MonthlyRent);
    }

    [Fact]
    public async Task AReservedBedIsReleasedWhenSomeoneMovesIn()
    {
        await _owner.PutAsJsonAsync($"/api/v1/beds/{BedA}", new { label = "A", status = "Reserved", defaultMonthlyRent = 8500 });

        await _owner.CreateTenantAsync("Rahul", moveIn: new { bedId = BedA, startDate = Today });
        var bed = (await _owner.GetRoomAsync(_room.Id)).Beds[0];

        Assert.Equal(BedStatus.Available, bed.Status);
        Assert.Equal(BedOccupancy.Occupied, bed.Occupancy);
    }

    [Fact]
    public async Task UnavailableBedsAndRoomsCannotBeAssigned()
    {
        await _owner.PutAsJsonAsync($"/api/v1/beds/{BedA}", new { label = "A", status = "Unavailable" });
        var other = await _owner.CreateRoomAsync(_property.Id, "202", capacity: 1, rent: 5000);
        await _owner.PutAsJsonAsync($"/api/v1/rooms/{other.Id}", new { roomNumber = "202", capacity = 1, status = "Unavailable" });
        var tenant = await _owner.CreateTenantAsync("Kiran");

        var bed = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-in", new { bedId = BedA, startDate = Today });
        var room = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-in", new { bedId = other.Beds[0].Id, startDate = Today });

        Assert.Equal("BED_UNAVAILABLE", (await bed.ReadErrorAsync()).Code);
        Assert.Equal("ROOM_UNAVAILABLE", (await room.ReadErrorAsync()).Code);
    }

    // ---- Move-out and moving beds ----------------------------------------------------------------

    [Fact]
    public async Task MoveOutFreesTheBedAndKeepsHistory()
    {
        var tenant = await _owner.CreateTenantAsync("Rahul", moveIn: new { bedId = BedA, startDate = Today.AddDays(-60) });

        var future = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-out", new { moveOutDate = Today.AddDays(1) });
        var beforeMoveIn = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-out", new { moveOutDate = Today.AddDays(-61) });
        var movedOut = await _owner.MoveOutAsync(tenant.Id, Today.AddDays(-1));

        Assert.Equal("DATE_IN_FUTURE", (await future.ReadErrorAsync()).Code);
        Assert.Equal("DATE_BEFORE_MOVE_IN", (await beforeMoveIn.ReadErrorAsync()).Code);
        Assert.Null(movedOut.CurrentTenancy);
        var past = Assert.Single(movedOut.History);
        Assert.Equal((AgreementStatus.Ended, AgreementEndReason.MovedOut, Today.AddDays(-1)), (past.Status, past.EndReason!.Value, past.EndDate!.Value));
        Assert.Equal(BedOccupancy.Vacant, (await _owner.GetRoomAsync(_room.Id)).Beds[0].Occupancy);
    }

    [Fact]
    public async Task MovingBedsEndsOneTenancyAndStartsAnother()
    {
        var tenant = await _owner.CreateTenantAsync("Rahul", moveIn: new { bedId = BedA, startDate = Today.AddDays(-30), securityDeposit = 5000, rentDueDay = 10 });
        await _owner.CreateTenantAsync("Blocker", moveIn: new { bedId = _room.Beds[2].Id, startDate = Today });

        var sameBed = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move", new { bedId = BedA, moveDate = Today });
        var occupied = await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move", new { bedId = _room.Beds[2].Id, moveDate = Today });
        var moved = await (await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move", new { bedId = BedB, moveDate = Today, monthlyRent = 9000 }))
            .ReadAsync<TenantDetail>();

        Assert.Equal("SAME_BED", (await sameBed.ReadErrorAsync()).Code);
        Assert.Equal("BED_OCCUPIED", (await occupied.ReadErrorAsync()).Code);
        Assert.Equal(2, moved.History.Count);
        var now = moved.CurrentTenancy!;
        Assert.Equal(("B", 9000m, 5000m, 10, Today), (now.BedLabel, now.MonthlyRent, now.SecurityDeposit, now.RentDueDay, now.StartDate));
        var before = moved.History.Single(h => h.Status == AgreementStatus.Ended);
        Assert.Equal((AgreementEndReason.Transferred, Today.AddDays(-1)), (before.EndReason!.Value, before.EndDate!.Value));

        var beds = (await _owner.GetRoomAsync(_room.Id)).Beds;
        Assert.Equal([BedOccupancy.Vacant, BedOccupancy.Occupied, BedOccupancy.Occupied], beds.Select(b => b.Occupancy));
    }

    [Fact]
    public async Task TenancyTermsCanBeChangedAndAreAudited()
    {
        var tenant = await _owner.CreateTenantAsync("Rahul", moveIn: new { bedId = BedA, startDate = Today });

        var updated = await (await _owner.PutAsJsonAsync($"/api/v1/tenants/{tenant.Id}/tenancy", new { monthlyRent = 9000, securityDeposit = 15000, rentDueDay = 1 }))
            .ReadAsync<TenantDetail>();

        Assert.Equal((9000m, 15000m, 1), (updated.CurrentTenancy!.MonthlyRent, updated.CurrentTenancy.SecurityDeposit, updated.CurrentTenancy.RentDueDay));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.EntityId == updated.CurrentTenancy.Id && a.Action == TenantAuditActions.TermsUpdated);
        Assert.Contains("8500", entry.Details, StringComparison.Ordinal);
        Assert.Contains("9000", entry.Details, StringComparison.Ordinal);
    }

    // ---- Rules that protect tenanted beds (Phase 3 hooks) ----------------------------------------

    [Fact]
    public async Task TenantedBedsRoomsAndPropertiesCannotBeArchivedOrDisabled()
    {
        var tenant = await _owner.CreateTenantAsync("Rahul", moveIn: new { bedId = BedA, startDate = Today.AddDays(-5) });

        var results = new Dictionary<string, HttpResponseMessage>
        {
            ["BED_HAS_TENANT"] = await _owner.PutAsJsonAsync($"/api/v1/beds/{BedA}", new { label = "A", status = "Unavailable" }),
            ["BED_HAS_TENANT "] = await _owner.DeleteAsync(new Uri($"/api/v1/beds/{BedA}", UriKind.Relative)),
            ["ROOM_HAS_TENANTS"] = await _owner.PutAsJsonAsync($"/api/v1/rooms/{_room.Id}", new { roomNumber = "201", capacity = 3, status = "Unavailable" }),
            ["ROOM_HAS_TENANTS "] = await _owner.DeleteAsync(new Uri($"/api/v1/rooms/{_room.Id}", UriKind.Relative)),
            ["PROPERTY_HAS_TENANTS"] = await _owner.DeleteAsync(new Uri($"/api/v1/properties/{_property.Id}", UriKind.Relative)),
            ["TENANT_HAS_ACTIVE_TENANCY"] = await _owner.DeleteAsync(new Uri($"/api/v1/tenants/{tenant.Id}", UriKind.Relative)),
        };
        foreach (var (code, response) in results)
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(code.Trim(), (await response.ReadErrorAsync()).Code);
        }

        await _owner.MoveOutAsync(tenant.Id, Today);
        Assert.Equal(HttpStatusCode.NoContent, (await _owner.DeleteAsync(new Uri($"/api/v1/tenants/{tenant.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _owner.DeleteAsync(new Uri($"/api/v1/rooms/{_room.Id}", UriKind.Relative))).StatusCode);
    }

    // ---- Listing and search ----------------------------------------------------------------------

    [Fact]
    public async Task SearchFindsByNamePhoneDigitsAndRoomNumber()
    {
        await _owner.CreateTenantAsync("Rahul Sharma", phone: "+91 98765 43210", moveIn: new { bedId = BedA, startDate = Today });
        await _owner.CreateTenantAsync("Amit Verma", phone: "080-2222-3333");

        Assert.Equal(["Rahul Sharma"], (await _owner.ListTenantsAsync("search=rahul")).Items.Select(t => t.FullName));
        Assert.Equal(["Rahul Sharma"], (await _owner.ListTenantsAsync("search=9876543210")).Items.Select(t => t.FullName));
        Assert.Equal(["Amit Verma"], (await _owner.ListTenantsAsync("search=22223333")).Items.Select(t => t.FullName));
        Assert.Equal(["Rahul Sharma"], (await _owner.ListTenantsAsync("search=201")).Items.Select(t => t.FullName));
    }

    [Fact]
    public async Task FiltersSeparateCurrentFormerAndUnassigned()
    {
        await _owner.CreateTenantAsync("Current", moveIn: new { bedId = BedA, startDate = Today });
        var former = await _owner.CreateTenantAsync("Former", moveIn: new { bedId = BedB, startDate = Today.AddDays(-10) });
        await _owner.MoveOutAsync(former.Id, Today);
        await _owner.CreateTenantAsync("Unassigned");

        Assert.Equal(["Current"], Names(await _owner.ListTenantsAsync("filter=Current")));
        Assert.Equal(["Former"], Names(await _owner.ListTenantsAsync("filter=Former")));
        Assert.Equal(["Unassigned"], Names(await _owner.ListTenantsAsync("filter=Unassigned")));
        Assert.Equal(["Current", "Former", "Unassigned"], Names(await _owner.ListTenantsAsync()));
        Assert.Equal(["Current"], Names(await _owner.ListTenantsAsync($"propertyId={_property.Id}")));
    }

    [Fact]
    public async Task TenantValidationReturnsFieldErrors()
    {
        var response = await _owner.PostAsJsonAsync("/api/v1/tenants", new { fullName = "R", phone = "abc", email = "nope" });
        var error = await response.ReadErrorAsync();

        Assert.Equal("VALIDATION_FAILED", error.Code);
        Assert.Contains("fullName", error.Errors!.Keys);
        Assert.Contains("phone", error.Errors.Keys);
        Assert.Contains("email", error.Errors.Keys);
    }

    // ---- Permissions and isolation ---------------------------------------------------------------

    [Fact]
    public async Task StaffNeedViewTenantsAndCannotChangeTenants()
    {
        var tenant = await _owner.CreateTenantAsync("Rahul", moveIn: new { bedId = BedA, startDate = Today });
        using var tenantViewer = await StaffClientAsync(StaffPermissions.ViewTenants, StaffPermissions.ViewProperties);
        using var propertyViewer = await StaffClientAsync(StaffPermissions.ViewProperties);

        Assert.Equal(HttpStatusCode.OK, (await tenantViewer.GetAsync(new Uri($"/api/v1/tenants/{tenant.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantViewer.PostAsJsonAsync("/api/v1/tenants", new { fullName = "Nope", phone = "9999999999" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantViewer.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move-out", new { moveOutDate = Today })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await propertyViewer.GetAsync(new Uri("/api/v1/tenants", UriKind.Relative))).StatusCode);

        // Room views show who lives in a bed only to people allowed to see tenants.
        var withNames = await tenantViewer.GetRoomAsync(_room.Id);
        var withoutNames = await propertyViewer.GetRoomAsync(_room.Id);
        Assert.Equal("Rahul", withNames.Beds[0].Tenant!.FullName);
        Assert.Null(withoutNames.Beds[0].Tenant);
        Assert.Equal(BedOccupancy.Occupied, withoutNames.Beds[0].Occupancy);
    }

    [Fact]
    public async Task AnotherOrganizationCannotSeeOrUseTenantsOrBeds()
    {
        var mine = await _owner.CreateTenantAsync("Mine", moveIn: new { bedId = BedA, startDate = Today });
        using var other = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Rival PG")).AccessToken);
        var theirs = await other.CreateTenantAsync("Theirs");

        var responses = new[]
        {
            await other.GetAsync(new Uri($"/api/v1/tenants/{mine.Id}", UriKind.Relative)),
            await other.PutAsJsonAsync($"/api/v1/tenants/{mine.Id}", new { fullName = "Hacked", phone = "9999999999" }),
            await other.PostAsJsonAsync($"/api/v1/tenants/{mine.Id}/move-out", new { moveOutDate = Today }),
            await other.PostAsJsonAsync($"/api/v1/tenants/{mine.Id}/move", new { bedId = BedB, moveDate = Today }),
            await other.PostAsJsonAsync($"/api/v1/tenants/{theirs.Id}/move-in", new { bedId = BedB, startDate = Today, monthlyRent = 1 }),
            await other.DeleteAsync(new Uri($"/api/v1/tenants/{mine.Id}", UriKind.Relative)),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal(["Theirs"], Names(await other.ListTenantsAsync()));
        Assert.Equal(BedOccupancy.Vacant, (await _owner.GetRoomAsync(_room.Id)).Beds[1].Occupancy);
        Assert.Equal("Mine", (await (await _owner.GetAsync(new Uri($"/api/v1/tenants/{mine.Id}", UriKind.Relative))).ReadAsync<TenantDetail>()).FullName);
    }

    private static List<string> Names(PagedResult<TenantSummary> page) => [.. page.Items.Select(t => t.FullName)];

    private async Task<HttpClient> StaffClientAsync(params StaffPermissions[] permissions)
    {
        var staff = await _owner.CreateStaffAsync("Staff", permissions);
        var auth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        return _factory.CreateClient(auth.AccessToken);
    }
}

internal static class TenantTestExtensions
{
    public static async Task<TenantDetail> CreateTenantAsync(this HttpClient owner, string fullName, string phone = "9876543210", object? moveIn = null)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/tenants", new { fullName, phone, moveIn }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<TenantDetail>();
    }

    public static async Task<TenantDetail> MoveOutAsync(this HttpClient owner, Guid tenantId, DateOnly moveOutDate)
    {
        var response = await owner.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/move-out", new { moveOutDate });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<TenantDetail>();
    }

    public static async Task<RoomDto> GetRoomAsync(this HttpClient client, Guid roomId)
    {
        var response = await client.GetAsync(new Uri($"/api/v1/rooms/{roomId}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<RoomDto>();
    }

    public static async Task<PagedResult<TenantSummary>> ListTenantsAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync(new Uri($"/api/v1/tenants?{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<TenantSummary>>();
    }
}

using System.Net;
using RentApp.Application.Auth;
using RentApp.Application.Dashboard;
using RentApp.Application.Properties;
using RentApp.Application.Rent;
using RentApp.Application.Tenants;
using RentApp.Domain.Users;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

/// <summary>
/// The dashboard, with the business date pinned to 12 Oct 2026. Room 201 has three beds at ₹8,500:
/// Asha has lived there since 20 Aug (owes Aug, Sep and Oct), Bala moved in on 1 Oct with rent due on the
/// 15th, and the third bed is empty.
/// </summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class DashboardTests(ContainersFixture containers) : IAsyncLifetime
{
    private static readonly DateTimeOffset Noon12Oct = new(2026, 10, 12, 6, 30, 0, TimeSpan.Zero);

    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _owner = null!;
    private PropertyDto _property = null!;
    private TenantDetail _asha = null!;
    private TenantDetail _bala = null!;

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.At(containers, Noon12Oct);
        _anonymous = _factory.CreateClient();
        _owner = _factory.CreateClient((await _anonymous.RegisterOwnerAsync()).AccessToken);
        (_property, _asha, _bala) = await SeedAsync(_owner);
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    private static async Task<(PropertyDto, TenantDetail, TenantDetail)> SeedAsync(HttpClient owner)
    {
        var property = await owner.CreatePropertyAsync("Sunrise PG");
        var room = await owner.CreateRoomAsync(property.Id, "201", capacity: 3, rent: 8500m);
        var asha = await owner.CreateTenantAsync("Asha", moveIn: new { bedId = room.Beds[0].Id, startDate = "2026-08-20", rentDueDay = 5 });
        var bala = await owner.CreateTenantAsync("Bala", moveIn: new { bedId = room.Beds[1].Id, startDate = "2026-10-01", rentDueDay = 15 });

        Assert.Equal(HttpStatusCode.Created, (await owner.RecordPaymentAsync(asha.Id, 12000m, date: "2026-10-10")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.RecordPaymentAsync(asha.Id, 1000m, date: "2026-09-30")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.RecordPaymentAsync(bala.Id, 2000m)).StatusCode);
        return (property, asha, bala);
    }

    [Fact]
    public async Task TheOwnerSeesOccupancyCollectionsAndWhoOwes()
    {
        var d = await _owner.DashboardAsync();

        Assert.Equal(new DateOnly(2026, 10, 12), d.Today);
        Assert.Equal((1, 1, 3, 2, 1), (d.Occupancy!.Properties, d.Occupancy.Rooms, d.Occupancy.Beds.TotalBeds, d.Occupancy.Beds.Occupied, d.Occupancy.Beds.Vacant));

        var rent = d.Rent!;
        // Received in October: ₹12,000 + ₹2,000. The ₹1,000 dated 30 Sep is September's money.
        Assert.Equal(new AmountCount(14000m, 2), rent.CollectedThisMonth);
        // October's dues: ₹17,000 billed, of which ₹2,000 is paid so far.
        Assert.Equal(new MonthProgress(new DateOnly(2026, 10, 1), 17000m, 0m, 17000m, 2000m, 15000m, 11), rent.ThisMonth);
        Assert.Equal(new AmountCount(19000m, 3), rent.Outstanding);
        Assert.Equal(new AmountCount(12500m, 2), rent.Overdue);
        Assert.Equal(new AmountCount(0m, 0), rent.DueToday);
        Assert.Equal(new AmountCount(6500m, 1), rent.DueThisWeek);
        Assert.Equal([(new DateOnly(2026, 9, 5), 4000m), (new DateOnly(2026, 10, 5), 8500m)], rent.OverdueList.Select(c => (c.DueDate, c.Balance)));
        Assert.Equal([2000m, 12000m, 1000m], rent.RecentPayments.Select(p => p.Amount));
    }

    [Fact]
    public async Task CachedFiguresAreReplacedAsSoonAsSomethingChanges()
    {
        var first = await _owner.DashboardAsync();
        var second = await _owner.DashboardAsync();
        Assert.Equal(first.GeneratedAt, second.GeneratedAt); // served from Redis

        await _owner.RecordPaymentAsync(_bala.Id, 6500m);
        var afterPayment = await _owner.DashboardAsync();
        Assert.NotEqual(first.GeneratedAt, afterPayment.GeneratedAt);
        Assert.Equal((new AmountCount(20500m, 3), 50, new AmountCount(0m, 0)),
            (afterPayment.Rent!.CollectedThisMonth, afterPayment.Rent.ThisMonth.PercentCollected, afterPayment.Rent.DueThisWeek));

        var october = (await _owner.ChargesAsync($"tenantId={_asha.Id}")).Items.Single(c => c.PeriodStart == new DateOnly(2026, 10, 1));
        await _owner.WaiveAsync(october.Id, 8500m, "Festival discount");
        var afterWaiver = await _owner.DashboardAsync();
        Assert.Equal((17000m, 8500m, 8500m, 100), (afterWaiver.Rent!.ThisMonth.Billed, afterWaiver.Rent.ThisMonth.Waived, afterWaiver.Rent.ThisMonth.Expected, afterWaiver.Rent.ThisMonth.PercentCollected));
        Assert.Equal(new AmountCount(4000m, 1), afterWaiver.Rent.Overdue);

        await _owner.CreateTenantAsync("Chitra", moveIn: new { bedId = (await VacantBedAsync()), startDate = "2026-10-12", rentDueDay = 12 });
        var afterMoveIn = await _owner.DashboardAsync();
        Assert.Equal((3, 0), (afterMoveIn.Occupancy!.Beds.Occupied, afterMoveIn.Occupancy.Beds.Vacant));
        Assert.Equal(new AmountCount(8500m, 1), afterMoveIn.Rent!.DueToday);
    }

    [Fact]
    public async Task StaffOnlySeeTheSectionsTheirPermissionsAllow()
    {
        using var beds = await StaffClientAsync(StaffPermissions.ViewProperties);
        using var money = await StaffClientAsync(StaffPermissions.ViewTenants);
        using var collector = await StaffClientAsync(StaffPermissions.RecordPayments);

        // The owner's figures are cached first; staff must still get only their own sections.
        await _owner.DashboardAsync();
        var b = await beds.DashboardAsync();
        var m = await money.DashboardAsync();
        var c = await collector.DashboardAsync();

        Assert.Equal((true, false), (b.Occupancy is not null, b.Rent is not null));
        Assert.Equal((false, true), (m.Occupancy is not null, m.Rent is not null));
        Assert.Equal((false, false), (c.Occupancy is not null, c.Rent is not null));
        Assert.Equal(19000m, m.Rent!.Outstanding.Amount);
    }

    [Fact]
    public async Task FiguresCanBeNarrowedToOneProperty()
    {
        var other = await _owner.CreatePropertyAsync("Other PG");
        var room = await _owner.CreateRoomAsync(other.Id, "1", capacity: 1, rent: 5000m);
        var dev = await _owner.CreateTenantAsync("Dev", moveIn: new { bedId = room.Beds[0].Id, startDate = "2026-10-01", rentDueDay = 5 });
        await _owner.RecordPaymentAsync(dev.Id, 5000m);

        var all = await _owner.DashboardAsync();
        var first = await _owner.DashboardAsync($"propertyId={_property.Id}");
        var second = await _owner.DashboardAsync($"propertyId={other.Id}");

        Assert.Equal((2, 4), (all.Occupancy!.Properties, all.Occupancy.Beds.TotalBeds));
        Assert.Equal((19000m, 14000m), (first.Rent!.Outstanding.Amount, first.Rent.CollectedThisMonth.Amount));
        Assert.Equal((0m, 5000m, 100), (second.Rent!.Outstanding.Amount, second.Rent.CollectedThisMonth.Amount, second.Rent.ThisMonth.PercentCollected));
        Assert.Equal(["Dev"], second.Rent.RecentPayments.Select(p => p.TenantName));
    }

    [Fact]
    public async Task AnotherOrganizationSeesOnlyItsOwnEmptyDashboard()
    {
        using var rival = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Rival PG")).AccessToken);

        var d = await rival.DashboardAsync();
        var mine = await rival.GetAsync(new Uri($"/api/v1/dashboard?propertyId={_property.Id}", UriKind.Relative));

        Assert.Equal((0, 0), (d.Occupancy!.Properties, d.Occupancy.Beds.TotalBeds));
        Assert.Equal((0m, 0m), (d.Rent!.Outstanding.Amount, d.Rent.CollectedThisMonth.Amount));
        Assert.Empty(d.Rent.OverdueList);
        Assert.Empty(d.Rent.RecentPayments);
        Assert.Equal(HttpStatusCode.NotFound, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync(new Uri("/api/v1/dashboard", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task TheDashboardStillWorksWhenRedisIsDown()
    {
        // Port 1 on loopback: nothing listens there, so Redis never connects.
        await using var factory = new RentAppFactory(containers.Postgres.GetConnectionString(), "127.0.0.1:1", businessNow: Noon12Oct);
        using var anonymous = factory.CreateClient();
        using var owner = factory.CreateClient((await anonymous.RegisterOwnerAsync()).AccessToken);
        var (_, _, bala) = await SeedAsync(owner);

        var before = await owner.DashboardAsync();
        await owner.RecordPaymentAsync(bala.Id, 6500m);
        var after = await owner.DashboardAsync();

        Assert.Equal(new AmountCount(14000m, 2), before.Rent!.CollectedThisMonth);
        Assert.Equal(new AmountCount(20500m, 3), after.Rent!.CollectedThisMonth);
    }

    private async Task<Guid> VacantBedAsync()
    {
        var response = await _owner.GetAsync(new Uri($"/api/v1/properties/{_property.Id}/rooms", UriKind.Relative));
        var rooms = await response.ReadAsync<List<RoomDto>>();
        return rooms.SelectMany(r => r.Beds).First(b => b.Occupancy == Domain.Properties.BedOccupancy.Vacant).Id;
    }

    private async Task<HttpClient> StaffClientAsync(StaffPermissions permission)
    {
        var staff = await _owner.CreateStaffAsync("Staff", permission);
        var auth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        return _factory.CreateClient(auth.AccessToken);
    }
}

internal static class DashboardTestExtensions
{
    public static async Task<DashboardDto> DashboardAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync(new Uri($"/api/v1/dashboard?{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<DashboardDto>();
    }
}

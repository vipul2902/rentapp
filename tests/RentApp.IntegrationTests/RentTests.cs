using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Api.Jobs;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Application.Properties;
using RentApp.Application.Rent;
using RentApp.Application.Tenants;
using RentApp.Domain.Rent;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

/// <summary>Rent engine end to end, with the business date pinned to 12 Oct 2026 (noon IST).</summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class RentTests(ContainersFixture containers) : IAsyncLifetime
{
    private static readonly DateTimeOffset Noon12Oct = new(2026, 10, 12, 6, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 12);

    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _owner = null!;
    private AuthResponse _ownerAuth = null!;
    private RoomDto _room = null!;

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.At(containers, Noon12Oct);
        _anonymous = _factory.CreateClient();
        _ownerAuth = await _anonymous.RegisterOwnerAsync();
        _owner = _factory.CreateClient(_ownerAuth.AccessToken);
        var property = await _owner.CreatePropertyAsync("Sunrise PG");
        _room = await _owner.CreateRoomAsync(property.Id, "201", capacity: 3, rent: 8500m);
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    private Guid Bed(int i) => _room.Beds[i].Id;

    private Task<TenantDetail> MoveInAsync(string name, int bed, string start, int dueDay = 5) =>
        _owner.CreateTenantAsync(name, moveIn: new { bedId = Bed(bed), startDate = start, rentDueDay = dueDay });

    // ---- Generation ------------------------------------------------------------------------------

    [Fact]
    public async Task BackdatedTenantOwesEveryMonthImmediately()
    {
        var tenant = await MoveInAsync("Rahul", 0, "2026-08-20");

        var charges = await _owner.ChargesAsync($"tenantId={tenant.Id}&filter=Overdue");

        Assert.Equal(3, charges.TotalCount);
        Assert.Equal(["2026-08-20", "2026-09-05", "2026-10-05"], charges.Items.Select(c => c.DueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.All(charges.Items, c => Assert.Equal((8500m, 8500m, RentStatus.Overdue), (c.Amount, c.Balance, c.Status)));
        Assert.Equal([53, 37, 7], charges.Items.Select(c => c.DaysOverdue));
        Assert.Equal(("Rahul", "201", "A"), (charges.Items[0].TenantName, charges.Items[0].RoomNumber, charges.Items[0].BedLabel));

        var detail = await _owner.GetTenantAsync(tenant.Id);
        Assert.Equal((25500m, 25500m), (detail.OutstandingAmount, detail.OverdueAmount));
    }

    [Fact]
    public async Task DueDayIsClampedAndOnlyNearDuesAppear()
    {
        var clamped = await MoveInAsync("Clamp", 0, "2026-09-01", dueDay: 31);
        var dueToday = await MoveInAsync("Today", 1, "2026-10-01", dueDay: 12);
        var upcoming = await MoveInAsync("Soon", 2, "2026-10-01", dueDay: 15);

        var c = (await _owner.ChargesAsync($"tenantId={clamped.Id}")).Items;
        var t = Assert.Single((await _owner.ChargesAsync($"tenantId={dueToday.Id}")).Items);
        var u = Assert.Single((await _owner.ChargesAsync($"tenantId={upcoming.Id}")).Items);

        // September's due day 31 becomes the 30th; October's 31st is more than a week away, so not yet created.
        Assert.Equal(new DateOnly(2026, 9, 30), Assert.Single(c).DueDate);
        Assert.Equal(RentStatus.DueToday, t.Status);
        Assert.Equal((RentStatus.Upcoming, new DateOnly(2026, 10, 15)), (u.Status, u.DueDate));
    }

    [Fact]
    public async Task SummaryAddsUpOverdueDueTodayAndThisWeek()
    {
        await MoveInAsync("Late", 0, "2026-10-01", dueDay: 5);
        await MoveInAsync("Today", 1, "2026-10-01", dueDay: 12);
        await MoveInAsync("Soon", 2, "2026-10-01", dueDay: 15);

        var summary = await (await _owner.GetAsync(new Uri("/api/v1/rent/summary", UriKind.Relative))).ReadAsync<RentSummary>();

        Assert.Equal(Today, summary.Today);
        Assert.Equal(new AmountCount(25500m, 3), summary.Outstanding);
        Assert.Equal(new AmountCount(8500m, 1), summary.Overdue);
        Assert.Equal(new AmountCount(8500m, 1), summary.DueToday);
        Assert.Equal(new AmountCount(17000m, 2), summary.DueThisWeek);
        Assert.Equal(new AmountCount(25500m, 3), summary.BilledThisMonth);
    }

    [Fact]
    public async Task GenerationIsIdempotentAndSafeUnderConcurrency()
    {
        // A tenancy inserted behind the API's back has no charges yet.
        var tenant = await _owner.CreateTenantAsync("Backfill");
        await InsertAgreementDirectlyAsync(tenant.Id, Bed(0), new DateOnly(2026, 7, 1));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            _owner.PostAsync(new Uri("/api/v1/rent/generate", UriKind.Relative), null)));
        var again = await (await _owner.PostAsync(new Uri("/api/v1/rent/generate", UriKind.Relative), null)).ReadAsync<GenerateResult>();

        var bodies = await Task.WhenAll(results.Select(r => r.Content.ReadAsStringAsync()));
        Assert.True(results.All(r => r.StatusCode == HttpStatusCode.OK), string.Join(" || ", bodies));
        var created = (await Task.WhenAll(results.Select(r => r.ReadAsync<GenerateResult>()))).Sum(r => r.Created);
        Assert.Equal(4, created); // Jul, Aug, Sep, Oct — created exactly once between the five runs
        Assert.Equal(0, again.Created);
        Assert.Equal(4, (await _owner.ChargesAsync($"tenantId={tenant.Id}")).TotalCount);
    }

    [Fact]
    public async Task BackgroundWorkerFillsInChargesForEveryOrganization()
    {
        var tenant = await _owner.CreateTenantAsync("Worker");
        await InsertAgreementDirectlyAsync(tenant.Id, Bed(1), new DateOnly(2026, 9, 1));

        var created = await _factory.Services.GetRequiredService<RentGenerationWorker>().RunOnceAsync(CancellationToken.None);

        Assert.True(created >= 2);
        Assert.Equal(2, (await _owner.ChargesAsync($"tenantId={tenant.Id}")).TotalCount);
    }

    // ---- Tenancy changes -------------------------------------------------------------------------

    [Fact]
    public async Task ACancelledBookingIsNeverCharged()
    {
        var booking = await MoveInAsync("Booked", 0, "2026-10-16");
        Assert.Equal(1, (await _owner.ChargesAsync($"tenantId={booking.Id}")).TotalCount); // due 16 Oct, within a week

        await _owner.MoveOutAsync(booking.Id, Today);

        Assert.Equal(0, (await _owner.ChargesAsync($"tenantId={booking.Id}")).TotalCount);
        var summary = await (await _owner.GetAsync(new Uri("/api/v1/rent/summary", UriKind.Relative))).ReadAsync<RentSummary>();
        Assert.Equal(0, summary.Outstanding.Count);
    }

    [Fact]
    public async Task MovingBedsMidMonthDoesNotChargeTwice()
    {
        var tenant = await MoveInAsync("Mover", 0, "2026-09-01");

        await _owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/move", new { bedId = Bed(1), moveDate = "2026-10-10", monthlyRent = 9500 });
        var charges = (await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items;

        Assert.Equal(2, charges.Count); // September and October, both on the original tenancy
        Assert.All(charges, c => Assert.Equal((8500m, "A"), (c.Amount, c.BedLabel)));
    }

    [Fact]
    public async Task MovingOutCancelsChargesForMonthsAfterTheLastDay()
    {
        // 28 Oct: November's charge (due the 3rd) is within a week, so it already exists.
        await using var factory = RentAppFactory.At(containers, new DateTimeOffset(2026, 10, 28, 6, 30, 0, TimeSpan.Zero));
        using var anonymous = factory.CreateClient();
        using var owner = factory.CreateClient((await anonymous.RegisterOwnerAsync()).AccessToken);
        var property = await owner.CreatePropertyAsync();
        var room = await owner.CreateRoomAsync(property.Id, "101", capacity: 1, rent: 7000m);
        var tenant = await owner.CreateTenantAsync("Leaver", moveIn: new { bedId = room.Beds[0].Id, startDate = "2026-10-01", rentDueDay = 3 });
        Assert.Equal(2, (await owner.ChargesAsync($"tenantId={tenant.Id}")).TotalCount);

        await owner.MoveOutAsync(tenant.Id, new DateOnly(2026, 10, 28));

        var remaining = Assert.Single((await owner.ChargesAsync($"tenantId={tenant.Id}")).Items);
        Assert.Equal(new DateOnly(2026, 10, 1), remaining.PeriodStart); // October is still owed in full
    }

    // ---- Waivers ---------------------------------------------------------------------------------

    [Fact]
    public async Task WaiversReduceTheBalanceButNeverBelowZero()
    {
        var tenant = await MoveInAsync("Waive", 0, "2026-10-01");
        var charge = Assert.Single((await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items);

        var partial = await (await _owner.WaiveAsync(charge.Id, 500, "Water outage")).ReadAsync<RentChargeDetail>();
        var tooMuch = await _owner.WaiveAsync(charge.Id, 9000, "Too much");
        var rest = await (await _owner.WaiveAsync(charge.Id, 8000, "Moved in late")).ReadAsync<RentChargeDetail>();

        Assert.Equal((8000m, RentStatus.Overdue), (partial.Charge.Balance, partial.Charge.Status));
        Assert.Equal("ADJUSTMENT_EXCEEDS_BALANCE", (await tooMuch.ReadErrorAsync()).Code);
        Assert.Equal((0m, 8500m, RentStatus.Waived), (rest.Charge.Balance, rest.Charge.AdjustedAmount, rest.Charge.Status));
        Assert.Equal(["Water outage", "Moved in late"], rest.Adjustments.Select(a => a.Reason));
        Assert.Equal(1, (await _owner.ChargesAsync("filter=Paid")).TotalCount);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.EntityId == charge.Id && a.Action == RentService.AdjustedAction));
    }

    [Fact]
    public async Task ConcurrentWaiversCannotOverSettleACharge()
    {
        var tenant = await MoveInAsync("Race", 0, "2026-10-01");
        var charge = Assert.Single((await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => _owner.WaiveAsync(charge.Id, 5000, $"Race {i}")));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        var detail = await (await _owner.GetAsync(new Uri($"/api/v1/rent/charges/{charge.Id}", UriKind.Relative))).ReadAsync<RentChargeDetail>();
        Assert.Equal((5000m, 3500m), (detail.Charge.AdjustedAmount, detail.Charge.Balance));
    }

    // ---- Lists, filters and access ---------------------------------------------------------------

    [Fact]
    public async Task OverdueListAndTenantFilterShowWhoOwes()
    {
        await MoveInAsync("Oldest", 0, "2026-08-01");
        await MoveInAsync("Recent", 1, "2026-10-01");
        await MoveInAsync("NotYet", 2, "2026-10-01", dueDay: 15);

        var overdue = await (await _owner.GetAsync(new Uri("/api/v1/rent/overdue", UriKind.Relative))).ReadAsync<PagedResult<RentChargeDto>>();
        var tenants = await _owner.ListTenantsAsync("filter=Overdue");

        // Oldest due date first; the two charges due on 5 Oct keep creation order.
        Assert.Equal(["Oldest", "Oldest", "Oldest", "Recent"], overdue.Items.Select(c => c.TenantName));
        Assert.Equal(["Oldest", "Recent"], tenants.Items.Select(t => t.FullName));
        Assert.Equal(25500m, tenants.Items[0].OverdueAmount);
    }

    [Fact]
    public async Task StaffNeedViewTenantsAndCannotGenerateOrWaive()
    {
        var tenant = await MoveInAsync("Rahul", 0, "2026-10-01");
        var charge = Assert.Single((await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items);
        using var viewer = await StaffClientAsync(StaffPermissions.ViewTenants);
        using var other = await StaffClientAsync(StaffPermissions.ViewProperties);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(new Uri("/api/v1/rent/charges", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(new Uri("/api/v1/rent/generate", UriKind.Relative), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.WaiveAsync(charge.Id, 100, "Not allowed")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(new Uri("/api/v1/rent/summary", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task AnotherOrganizationCannotSeeOrWaiveCharges()
    {
        var tenant = await MoveInAsync("Mine", 0, "2026-09-01");
        var charge = (await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items[0];
        using var rival = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Rival PG")).AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await rival.GetAsync(new Uri($"/api/v1/rent/charges/{charge.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.WaiveAsync(charge.Id, 100, "Hijack")).StatusCode);
        Assert.Equal(0, (await rival.ChargesAsync()).TotalCount);
        Assert.Equal(0, (await (await rival.PostAsync(new Uri("/api/v1/rent/generate", UriKind.Relative), null)).ReadAsync<GenerateResult>()).Created);
        var mine = await (await _owner.GetAsync(new Uri($"/api/v1/rent/charges/{charge.Id}", UriKind.Relative))).ReadAsync<RentChargeDetail>();
        Assert.Equal(8500m, mine.Charge.Balance);
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private async Task InsertAgreementDirectlyAsync(Guid tenantId, Guid bedId, DateOnly start)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUserOverride>().User = new SystemCurrentUser(_ownerAuth.User.Organization.Id);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bed = await db.Beds.SingleAsync(b => b.Id == bedId);
        db.RentAgreements.Add(RentAgreement.Start(_ownerAuth.User.Organization.Id, tenantId, _room.PropertyId, bed.RoomId, bed.Id, 8500m, 0m, 5, start));
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> StaffClientAsync(params StaffPermissions[] permissions)
    {
        var staff = await _owner.CreateStaffAsync("Staff", permissions);
        var auth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        return _factory.CreateClient(auth.AccessToken);
    }
}

internal static class RentTestExtensions
{
    public static async Task<PagedResult<RentChargeDto>> ChargesAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync(new Uri($"/api/v1/rent/charges?pageSize=100&{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<RentChargeDto>>();
    }

    public static Task<HttpResponseMessage> WaiveAsync(this HttpClient client, Guid chargeId, decimal amount, string reason) =>
        client.PostAsJsonAsync($"/api/v1/rent/charges/{chargeId}/adjustments", new { amount, reason });

    public static async Task<TenantDetail> GetTenantAsync(this HttpClient client, Guid tenantId)
    {
        var response = await client.GetAsync(new Uri($"/api/v1/tenants/{tenantId}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<TenantDetail>();
    }
}

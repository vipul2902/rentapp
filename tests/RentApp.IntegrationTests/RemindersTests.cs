using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Properties;
using RentApp.Application.Reminders;
using RentApp.Application.Rent;
using RentApp.Application.Tenants;
using RentApp.Domain.Reminders;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

/// <summary>
/// Reminders with the business date pinned to 12 Oct 2026. One tenant per stage: Late (Sep and Oct unpaid,
/// 37 and 7 days late), Three (3 days late), Grace (1 day late), Today (due today), Soon (due in 2 days) and
/// Far (due in 7 days).
/// </summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class RemindersTests(ContainersFixture containers) : IAsyncLifetime
{
    private static readonly DateTimeOffset Noon12Oct = new(2026, 10, 12, 6, 30, 0, TimeSpan.Zero);

    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _owner = null!;
    private AuthResponse _ownerAuth = null!;
    private readonly Dictionary<string, TenantDetail> _tenants = [];

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.At(containers, Noon12Oct);
        _anonymous = _factory.CreateClient();
        _ownerAuth = await _anonymous.RegisterOwnerAsync();
        _owner = _factory.CreateClient(_ownerAuth.AccessToken);
        var property = await _owner.CreatePropertyAsync("Sunrise PG");
        var room = await _owner.CreateRoomAsync(property.Id, "201", capacity: 6, rent: 8500m);
        (string Name, string Start, int DueDay)[] people =
            [("Late Lal", "2026-09-01", 5), ("Three", "2026-10-01", 9), ("Grace", "2026-10-01", 11), ("Today Tara", "2026-10-01", 12), ("Soon", "2026-10-01", 14), ("Far", "2026-10-01", 19)];
        for (var i = 0; i < people.Length; i++)
        {
            var (name, start, dueDay) = people[i];
            _tenants[name] = await _owner.CreateTenantAsync(name, moveIn: new { bedId = room.Beds[i].Id, startDate = start, rentDueDay = dueDay });
        }
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<RentChargeDto> ChargeOfAsync(string tenant, int month = 10) =>
        (await _owner.ChargesAsync($"tenantId={_tenants[tenant].Id}")).Items.Single(c => c.PeriodStart.Month == month);

    [Fact]
    public async Task TheQueueSuggestsEachStageWithAReadyMessage()
    {
        var queue = await _owner.ReminderQueueAsync();

        Assert.Equal((1, 1, 1, 2), (queue.Upcoming, queue.DueToday, queue.Overdue, queue.LongOverdue));
        // Most overdue first. Grace (1 day late) waits; Far (7 days ahead) is too early.
        Assert.Equal(
            [("Late Lal", ReminderType.LongOverdue), ("Late Lal", ReminderType.LongOverdue), ("Three", ReminderType.Overdue), ("Today Tara", ReminderType.DueToday), ("Soon", ReminderType.Upcoming)],
            queue.Items.Select(s => (s.Charge.TenantName, s.Type)));
        Assert.Equal(
            "Hi Today, your rent of ₹8,500 for October 2026 is due today. Please make the payment at your earliest convenience. Thank you! – Sunrise PG",
            queue.Items.Single(s => s.Type == ReminderType.DueToday).Message);
        Assert.Equal(2, (await _owner.ReminderQueueAsync("type=LongOverdue")).Items.Count);
        Assert.Equal(5, (await _owner.DashboardAsync()).RemindersToSend);
    }

    [Fact]
    public async Task SendingOrCopyingTakesADueOffTheQueue()
    {
        var today = await ChargeOfAsync("Today Tara");
        var soon = await ChargeOfAsync("Soon");

        var sent = await (await _owner.CreateReminderAsync(today.Id, "WhatsApp")).ReadAsync<ReminderDto>();
        var copiedResponse = await _owner.CreateReminderAsync(soon.Id, "Copy", message: "Hi Soon, gentle reminder about rent. – Sunrise PG");
        Assert.Equal(HttpStatusCode.Created, copiedResponse.StatusCode);
        var copied = await copiedResponse.ReadAsync<ReminderDto>();

        Assert.Equal((ReminderStatus.Sent, ReminderType.DueToday, ReminderChannel.WhatsApp, "Asha Owner"), (sent.Status, sent.Type, sent.Channel, sent.CreatedByName!));
        Assert.NotNull(sent.SentAt);
        Assert.StartsWith("Hi Today, your rent of ₹8,500", sent.Message, StringComparison.Ordinal);
        Assert.Equal((ReminderStatus.Prepared, "Hi Soon, gentle reminder about rent. – Sunrise PG"), (copied.Status, copied.Message));
        var queue = await _owner.ReminderQueueAsync();
        Assert.DoesNotContain(queue.Items, s => s.Charge.Id == today.Id || s.Charge.Id == soon.Id);

        var marked = await (await _owner.PostAsync(new Uri($"/api/v1/reminders/{copied.Id}/sent", UriKind.Relative), null)).ReadAsync<ReminderDto>();
        var again = await _owner.PostAsync(new Uri($"/api/v1/reminders/{copied.Id}/sent", UriKind.Relative), null);
        Assert.Equal(ReminderStatus.Sent, marked.Status);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var history = await _owner.RemindersAsync($"tenantId={_tenants["Soon"].Id}");
        Assert.Equal((1, ReminderStatus.Sent), (history.TotalCount, history.Items[0].Status));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.AuditLogs.IgnoreQueryFilters().Where(a => a.EntityId == copied.Id).Select(a => a.Action).ToListAsync();
        Assert.Equal([ReminderService.CreatedAction, ReminderService.SentAction], actions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnyOwedDueCanBePreviewedButSettledOnesCannotBeReminded()
    {
        var grace = await ChargeOfAsync("Grace");
        var preview = await (await _owner.GetAsync(new Uri($"/api/v1/reminders/preview?rentChargeId={grace.Id}", UriKind.Relative))).ReadAsync<ReminderPreview>();
        Assert.Equal(ReminderType.Overdue, preview.Type);
        Assert.Contains("was due on 11 Oct 2026 and is now 1 day overdue", preview.Message, StringComparison.Ordinal);
        Assert.Empty(preview.History);

        await _owner.RecordPaymentAsync(_tenants["Grace"].Id, 8500m);
        var settled = await _owner.GetAsync(new Uri($"/api/v1/reminders/preview?rentChargeId={grace.Id}", UriKind.Relative));
        var create = await _owner.CreateReminderAsync(grace.Id, "Share");
        var unknown = await _owner.CreateReminderAsync(Guid.NewGuid(), "Share");
        var tooLong = await _owner.CreateReminderAsync((await ChargeOfAsync("Soon")).Id, "Share", message: new string('x', 1001));

        Assert.Equal(("NOTHING_TO_REMIND", "NOTHING_TO_REMIND"), ((await settled.ReadErrorAsync()).Code, (await create.ReadErrorAsync()).Code));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task PartlyPaidDuesAskForTheRemainingAmount()
    {
        await _owner.RecordPaymentAsync(_tenants["Three"].Id, 5000m);
        var three = await ChargeOfAsync("Three");

        var preview = await (await _owner.GetAsync(new Uri($"/api/v1/reminders/preview?rentChargeId={three.Id}", UriKind.Relative))).ReadAsync<ReminderPreview>();

        Assert.Contains("the remaining rent of ₹3,500 for October 2026", preview.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LongOverdueDuesComeBackAWeekLater()
    {
        var late = await ChargeOfAsync("Late Lal");
        await _owner.CreateReminderAsync(late.Id, "Sms");
        Assert.DoesNotContain((await _owner.ReminderQueueAsync()).Items, s => s.Charge.Id == late.Id);

        // Six days later it is still too soon; seven days later it is suggested again.
        foreach (var (day, expected) in new[] { (18, false), (19, true) })
        {
            await using var later = RentAppFactory.At(containers, new DateTimeOffset(2026, 10, day, 6, 30, 0, TimeSpan.Zero));
            using var owner = later.CreateClient(_ownerAuth.AccessToken);
            var queue = await owner.ReminderQueueAsync();
            Assert.Equal(expected, queue.Items.Any(s => s.Charge.Id == late.Id && s.Type == ReminderType.LongOverdue));
        }
    }

    [Fact]
    public async Task RemindingNeedsSendRemindersWhileHistoryAlsoWorksWithViewTenants()
    {
        var today = await ChargeOfAsync("Today Tara");
        using var reminder = await StaffClientAsync(StaffPermissions.SendReminders);
        using var viewer = await StaffClientAsync(StaffPermissions.ViewTenants);
        using var other = await StaffClientAsync(StaffPermissions.ViewProperties);

        Assert.Equal(HttpStatusCode.OK, (await reminder.GetAsync(new Uri("/api/v1/reminders/queue", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await reminder.CreateReminderAsync(today.Id, "Share")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(new Uri("/api/v1/reminders/queue", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.CreateReminderAsync(today.Id, "Share")).StatusCode);
        Assert.Equal(1, (await viewer.RemindersAsync()).TotalCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(new Uri("/api/v1/reminders", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task AnotherOrganizationCannotSeeOrRemindAboutMyTenants()
    {
        var today = await ChargeOfAsync("Today Tara");
        var created = await _owner.CreateReminderAsync(today.Id, "Copy");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var mine = await created.ReadAsync<ReminderDto>();
        using var rival = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Rival PG")).AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await rival.GetAsync(new Uri($"/api/v1/reminders/preview?rentChargeId={today.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.CreateReminderAsync(today.Id, "Share")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.PostAsync(new Uri($"/api/v1/reminders/{mine.Id}/sent", UriKind.Relative), null)).StatusCode);
        Assert.Equal(0, (await rival.RemindersAsync()).TotalCount);
        Assert.Empty((await rival.ReminderQueueAsync()).Items);
        Assert.Equal(ReminderStatus.Prepared, (await _owner.RemindersAsync()).Items.Single().Status);
    }

    private async Task<HttpClient> StaffClientAsync(StaffPermissions permission)
    {
        var staff = await _owner.CreateStaffAsync("Staff", permission);
        var auth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        return _factory.CreateClient(auth.AccessToken);
    }
}

internal static class ReminderTestExtensions
{
    public static async Task<ReminderSuggestions> ReminderQueueAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync(new Uri($"/api/v1/reminders/queue?{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<ReminderSuggestions>();
    }

    public static Task<HttpResponseMessage> CreateReminderAsync(this HttpClient client, Guid rentChargeId, string channel, string? message = null) =>
        client.PostAsJsonAsync("/api/v1/reminders", new { rentChargeId, channel, message });

    public static async Task<PagedResult<ReminderDto>> RemindersAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync(new Uri($"/api/v1/reminders?{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<ReminderDto>>();
    }
}

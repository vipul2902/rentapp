using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Payments;
using RentApp.Application.Properties;
using RentApp.Application.Rent;
using RentApp.Application.Tenants;
using RentApp.Domain.Payments;
using RentApp.Domain.Rent;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Persistence;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

/// <summary>
/// Payments and receipts end to end, with the business date pinned to 12 Oct 2026. A tenant who moved in on
/// 20 Aug owes three months of ₹8,500 (due 20 Aug, 5 Sep and 5 Oct), ₹25,500 in all.
/// </summary>
[Collection(IntegrationTestGroup.Name)]
public sealed class PaymentsTests(ContainersFixture containers) : IAsyncLifetime
{
    private static readonly DateTimeOffset Noon12Oct = new(2026, 10, 12, 6, 30, 0, TimeSpan.Zero);

    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _owner = null!;
    private RoomDto _room = null!;

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.At(containers, Noon12Oct);
        _anonymous = _factory.CreateClient();
        _owner = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Sunrise Living")).AccessToken);
        var property = await _owner.CreatePropertyAsync("Sunrise PG");
        _room = await _owner.CreateRoomAsync(property.Id, "201", capacity: 3, rent: 8500m);
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    private Task<TenantDetail> MoveInAsync(string name, int bed = 0, string start = "2026-08-20") =>
        _owner.CreateTenantAsync(name, moveIn: new { bedId = _room.Beds[bed].Id, startDate = start, rentDueDay = 5 });

    // ---- Recording -------------------------------------------------------------------------------

    [Fact]
    public async Task APaymentSettlesTheOldestDuesFirstAndIssuesAReceipt()
    {
        var tenant = await MoveInAsync("Rahul Sharma");

        var response = await _owner.RecordPaymentAsync(tenant.Id, 12000m, reference: "UPI-4471");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.ReadAsync<RecordPaymentResult>();

        var charges = (await _owner.ChargesAsync($"tenantId={tenant.Id}&filter=All")).Items.OrderBy(c => c.DueDate).ToList();
        // A partly paid charge that is past due still reads as Overdue (the balance is late).
        Assert.Equal([(8500m, 0m, RentStatus.Paid), (3500m, 5000m, RentStatus.Overdue), (0m, 8500m, RentStatus.Overdue)],
            charges.Select(c => (c.PaidAmount, c.Balance, c.Status)));
        Assert.Equal([8500m, 3500m], result.Payment.Allocations.Select(a => a.Amount));
        Assert.Equal((13500m, PaymentStatus.Recorded, "Asha Owner"), ((await _owner.GetTenantAsync(tenant.Id)).OutstandingAmount, result.Payment.Status, result.Payment.RecordedByName!));

        var receipt = result.Receipt;
        Assert.Equal("REC-2026-000001", receipt.ReceiptNumber);
        Assert.Equal(("Sunrise Living", "Sunrise PG", "Rahul Sharma", "201", "A"),
            (receipt.OrganizationName, receipt.PropertyName, receipt.TenantName, receipt.RoomNumber, receipt.BedLabel));
        Assert.Equal(("August 2026 – September 2026", 12000m, PaymentMethod.Upi, "UPI-4471", false),
            (receipt.PeriodLabel, receipt.Amount, receipt.Method, receipt.ReferenceNumber, receipt.IsVoid));
        Assert.Equal((new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12)), (receipt.IssuedOn, receipt.PaymentDate));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.EntityId == result.Payment.Id && a.Action == PaymentService.RecordedAction));
    }

    [Fact]
    public async Task InvalidPaymentsAreRejectedWithoutRecordingAnything()
    {
        var tenant = await MoveInAsync("Rahul");
        var noDues = await _owner.CreateTenantAsync("Nobody");

        var tooMuch = await _owner.RecordPaymentAsync(tenant.Id, 25500.01m);
        var future = await _owner.RecordPaymentAsync(tenant.Id, 100m, date: "2026-10-13");
        var zero = await _owner.RecordPaymentAsync(tenant.Id, 0m);
        var paisa = await _owner.RecordPaymentAsync(tenant.Id, 10.005m);
        var nothingOwed = await _owner.RecordPaymentAsync(noDues.Id, 100m);
        var unknownTenant = await _owner.RecordPaymentAsync(Guid.NewGuid(), 100m);

        var tooMuchError = await tooMuch.ReadErrorAsync();
        Assert.Equal((HttpStatusCode.BadRequest, "PAYMENT_EXCEEDS_OUTSTANDING"), (tooMuch.StatusCode, tooMuchError.Code));
        Assert.Contains("₹25,500", tooMuchError.Message, StringComparison.Ordinal);
        Assert.Equal("DATE_IN_FUTURE", (await future.ReadErrorAsync()).Code);
        Assert.Equal(("VALIDATION_FAILED", "VALIDATION_FAILED"), ((await zero.ReadErrorAsync()).Code, (await paisa.ReadErrorAsync()).Code));
        Assert.Equal((HttpStatusCode.Conflict, "NOTHING_OUTSTANDING"), (nothingOwed.StatusCode, (await nothingOwed.ReadErrorAsync()).Code));
        Assert.Equal(HttpStatusCode.NotFound, unknownTenant.StatusCode);
        Assert.Equal(0, (await _owner.PaymentsAsync()).TotalCount);
        Assert.Equal(25500m, (await _owner.GetTenantAsync(tenant.Id)).OutstandingAmount);
    }

    [Fact]
    public async Task ChosenDuesArePaidInsteadOfTheOldest()
    {
        var tenant = await MoveInAsync("Picky");
        var charges = (await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items;
        var october = charges.Single(c => c.PeriodStart == new DateOnly(2026, 10, 1));

        var paid = await (await _owner.RecordPaymentAsync(tenant.Id, 8500m, chargeIds: [october.Id])).ReadAsync<RecordPaymentResult>();
        var again = await _owner.RecordPaymentAsync(tenant.Id, 100m, chargeIds: [october.Id]);
        var stranger = await _owner.RecordPaymentAsync(tenant.Id, 100m, chargeIds: [Guid.NewGuid()]);

        Assert.Equal(october.Id, Assert.Single(paid.Payment.Allocations).RentChargeId);
        Assert.Equal("October 2026", paid.Receipt.PeriodLabel);
        Assert.Equal(("CHARGE_NOT_PAYABLE", "CHARGE_NOT_PAYABLE"), ((await again.ReadErrorAsync()).Code, (await stranger.ReadErrorAsync()).Code));
        Assert.Equal(17000m, (await _owner.GetTenantAsync(tenant.Id)).OverdueAmount);
    }

    // ---- Financial integrity under retries and concurrency ---------------------------------------

    [Fact]
    public async Task RetriesWithTheSameIdempotencyKeyRecordThePaymentOnce()
    {
        var tenant = await MoveInAsync("Retry");
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _owner.RecordPaymentAsync(tenant.Id, 8500m, key: key)));
        var reused = await _owner.RecordPaymentAsync(tenant.Id, 9000m, key: key);

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        Assert.True(responses.All(r => r.StatusCode == HttpStatusCode.Created), string.Join(" || ", bodies));
        var results = await Task.WhenAll(responses.Select(r => r.ReadAsync<RecordPaymentResult>()));
        Assert.Single(results.Select(r => r.Payment.Id).Distinct());
        Assert.Single(results.Select(r => r.Receipt.ReceiptNumber).Distinct());
        Assert.Equal(1, (await _owner.PaymentsAsync($"tenantId={tenant.Id}")).TotalCount);
        Assert.Equal(17000m, (await _owner.GetTenantAsync(tenant.Id)).OutstandingAmount);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await reused.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task ConcurrentPaymentsCannotOverSettleAndGetGapFreeReceiptNumbers()
    {
        var tenant = await MoveInAsync("Rush");

        // Five payments of one month's rent race for three months of dues; the last two find nothing owed.
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _owner.RecordPaymentAsync(tenant.Id, 8500m)));

        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var numbers = (await Task.WhenAll(responses.Where(r => r.IsSuccessStatusCode).Select(r => r.ReadAsync<RecordPaymentResult>())))
            .Select(r => r.Receipt.ReceiptNumber).Order(StringComparer.Ordinal);
        Assert.Equal(["REC-2026-000001", "REC-2026-000002", "REC-2026-000003"], numbers);
        var charges = (await _owner.ChargesAsync($"tenantId={tenant.Id}&filter=All")).Items;
        Assert.All(charges, c => Assert.Equal((8500m, 0m), (c.PaidAmount, c.Balance)));
    }

    [Fact]
    public async Task APaymentRacingAWaiverCanNeverOverSettleACharge()
    {
        var tenant = await MoveInAsync("Overlap", start: "2026-10-01");
        var charge = Assert.Single((await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items);

        var results = await Task.WhenAll(_owner.RecordPaymentAsync(tenant.Id, 8500m), _owner.WaiveAsync(charge.Id, 5000m, "Discount"));

        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        var detail = await (await _owner.GetAsync(new Uri($"/api/v1/rent/charges/{charge.Id}", UriKind.Relative))).ReadAsync<RentChargeDetail>();
        Assert.True(detail.Charge.PaidAmount + detail.Charge.AdjustedAmount <= 8500m);
        Assert.Equal(detail.Charge.Amount - detail.Charge.PaidAmount - detail.Charge.AdjustedAmount, detail.Charge.Balance);
    }

    [Fact]
    public async Task ReceiptNumbersAreCountedPerOrganization()
    {
        var first = await MoveInAsync("One", 0);
        var second = await MoveInAsync("Two", 1);
        using var rival = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Rival PG")).AccessToken);
        var rivalProperty = await rival.CreatePropertyAsync("Rival House");
        var rivalRoom = await rival.CreateRoomAsync(rivalProperty.Id, "1", capacity: 1, rent: 5000m);
        var rivalTenant = await rival.CreateTenantAsync("Them", moveIn: new { bedId = rivalRoom.Beds[0].Id, startDate = "2026-10-01", rentDueDay = 5 });

        var a = await (await _owner.RecordPaymentAsync(first.Id, 100m)).ReadAsync<RecordPaymentResult>();
        var b = await (await _owner.RecordPaymentAsync(second.Id, 100m)).ReadAsync<RecordPaymentResult>();
        var c = await (await rival.RecordPaymentAsync(rivalTenant.Id, 100m)).ReadAsync<RecordPaymentResult>();

        Assert.Equal(["REC-2026-000001", "REC-2026-000002", "REC-2026-000001"], new[] { a, b, c }.Select(r => r.Receipt.ReceiptNumber));
    }

    // ---- Voiding ---------------------------------------------------------------------------------

    [Fact]
    public async Task VoidingGivesTheDuesBackAndMarksTheReceipt()
    {
        var tenant = await MoveInAsync("Oops");
        var recorded = await (await _owner.RecordPaymentAsync(tenant.Id, 12000m)).ReadAsync<RecordPaymentResult>();

        var noReason = await _owner.VoidPaymentAsync(recorded.Payment.Id, "");
        var voided = await _owner.VoidPaymentAsync(recorded.Payment.Id, "Entered twice by mistake");
        var twice = await _owner.VoidPaymentAsync(recorded.Payment.Id, "Again");

        Assert.Equal("VALIDATION_FAILED", (await noReason.ReadErrorAsync()).Code);
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        var payment = await voided.ReadAsync<PaymentDto>();
        Assert.Equal((PaymentStatus.Voided, "Entered twice by mistake"), (payment.Status, payment.VoidReason!));
        Assert.Equal("PAYMENT_ALREADY_VOIDED", (await twice.ReadErrorAsync()).Code);

        Assert.Equal(25500m, (await _owner.GetTenantAsync(tenant.Id)).OutstandingAmount);
        var receipt = await (await _owner.GetAsync(new Uri($"/api/v1/receipts/{recorded.Receipt.Id}", UriKind.Relative))).ReadAsync<ReceiptDto>();
        Assert.True(receipt.IsVoid);
        var august = (await _owner.ChargesAsync($"tenantId={tenant.Id}")).Items.Single(c => c.PeriodStart == new DateOnly(2026, 8, 1));
        var history = await (await _owner.GetAsync(new Uri($"/api/v1/rent/charges/{august.Id}", UriKind.Relative))).ReadAsync<RentChargeDetail>();
        Assert.Equal((PaymentStatus.Voided, 8500m, "REC-2026-000001"),
            (Assert.Single(history.Payments).Status, history.Payments[0].AllocatedAmount, history.Payments[0].ReceiptNumber));

        // The dues can be paid again, with the next receipt number.
        var repaid = await (await _owner.RecordPaymentAsync(tenant.Id, 8500m)).ReadAsync<RecordPaymentResult>();
        Assert.Equal("REC-2026-000002", repaid.Receipt.ReceiptNumber);
        Assert.Equal(2, (await _owner.PaymentsAsync($"tenantId={tenant.Id}")).TotalCount);
        Assert.Equal(1, (await _owner.PaymentsAsync($"tenantId={tenant.Id}&includeVoided=false")).TotalCount);
    }

    [Fact]
    public async Task TheDatabaseRefusesToEditOrDeleteFinancialHistory()
    {
        var tenant = await MoveInAsync("Permanent");
        var recorded = await (await _owner.RecordPaymentAsync(tenant.Id, 8500m)).ReadAsync<RecordPaymentResult>();
        var paymentId = recorded.Payment.Id;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        async Task<bool> Refused(FormattableString sql)
        {
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync(sql);
                return false;
            }
            catch (Npgsql.PostgresException e) when (e.SqlState == Npgsql.PostgresErrorCodes.RestrictViolation)
            {
                return true;
            }
        }

        // Even direct SQL (a bug, a script, a person with a database client) cannot rewrite history.
        Assert.True(await Refused($"DELETE FROM payments WHERE id = {paymentId}"));
        Assert.True(await Refused($"UPDATE payments SET amount = 1 WHERE id = {paymentId}"));
        Assert.True(await Refused($"DELETE FROM payment_allocations WHERE payment_id = {paymentId}"));
        Assert.True(await Refused($"UPDATE receipts SET amount = 1 WHERE payment_id = {paymentId}"));
        Assert.True(await Refused($"DELETE FROM rent_charges WHERE tenant_id = {tenant.Id}"));
        Assert.True(await Refused($"DELETE FROM audit_logs WHERE entity_id = {paymentId}"));

        // Voiding (once) is the only change a payment allows.
        Assert.Equal(HttpStatusCode.OK, (await _owner.VoidPaymentAsync(paymentId, "Allowed correction")).StatusCode);
        Assert.True(await Refused($"UPDATE payments SET status = 'Recorded', voided_at = NULL, void_reason = NULL WHERE id = {paymentId}"));
    }

    // ---- Receipts --------------------------------------------------------------------------------

    [Fact]
    public async Task TheReceiptDownloadsAsAPrivatePdf()
    {
        var tenant = await MoveInAsync("Priya Nair");
        var recorded = await (await _owner.RecordPaymentAsync(tenant.Id, 25500m)).ReadAsync<RecordPaymentResult>();

        var response = await _owner.GetAsync(new Uri($"/api/v1/receipts/{recorded.Receipt.Id}/pdf", UriKind.Relative));
        await _owner.VoidPaymentAsync(recorded.Payment.Id, "Testing the void stamp");
        var voidedPdf = await _owner.GetAsync(new Uri($"/api/v1/receipts/{recorded.Receipt.Id}/pdf", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("REC-2026-000001.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-"u8.ToArray(), bytes[..5]);
        Assert.Equal(HttpStatusCode.OK, voidedPdf.StatusCode);
        Assert.NotEqual(bytes.Length, (await voidedPdf.Content.ReadAsByteArrayAsync()).Length);
    }

    // ---- Access ----------------------------------------------------------------------------------

    [Fact]
    public async Task StaffPermissionsControlRecordingReceiptsAndVoiding()
    {
        var tenant = await MoveInAsync("Rahul");
        using var collector = await StaffClientAsync(StaffPermissions.RecordPayments);
        using var receiptsOnly = await StaffClientAsync(StaffPermissions.GenerateReceipts);
        using var viewer = await StaffClientAsync(StaffPermissions.ViewTenants);

        var recorded = await collector.RecordPaymentAsync(tenant.Id, 1000m);
        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);
        var result = await recorded.ReadAsync<RecordPaymentResult>();
        Assert.Equal("Staff", result.Payment.RecordedByName);

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.RecordPaymentAsync(tenant.Id, 1000m)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receiptsOnly.RecordPaymentAsync(tenant.Id, 1000m)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await collector.VoidPaymentAsync(result.Payment.Id, "Not mine to void")).StatusCode);

        var receiptUri = new Uri($"/api/v1/receipts/{result.Receipt.Id}/pdf", UriKind.Relative);
        Assert.Equal(HttpStatusCode.OK, (await collector.GetAsync(receiptUri)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await receiptsOnly.GetAsync(receiptUri)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(receiptUri)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(new Uri($"/api/v1/payments/{result.Payment.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await collector.GetAsync(new Uri("/api/v1/payments", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task AnotherOrganizationCannotTouchPaymentsOrReceipts()
    {
        var tenant = await MoveInAsync("Mine");
        var recorded = await (await _owner.RecordPaymentAsync(tenant.Id, 5000m)).ReadAsync<RecordPaymentResult>();
        using var rival = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Rival PG")).AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await rival.RecordPaymentAsync(tenant.Id, 100m)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.GetAsync(new Uri($"/api/v1/payments/{recorded.Payment.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.VoidPaymentAsync(recorded.Payment.Id, "Hijack")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.GetAsync(new Uri($"/api/v1/receipts/{recorded.Receipt.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.GetAsync(new Uri($"/api/v1/receipts/{recorded.Receipt.Id}/pdf", UriKind.Relative))).StatusCode);
        Assert.Equal(0, (await rival.PaymentsAsync()).TotalCount);
        Assert.Equal(PaymentStatus.Recorded, (await (await _owner.GetAsync(new Uri($"/api/v1/payments/{recorded.Payment.Id}", UriKind.Relative))).ReadAsync<PaymentDto>()).Status);
    }

    [Fact]
    public async Task PaymentsCanBeListedByTenantAndProperty()
    {
        var first = await MoveInAsync("First", 0);
        var second = await MoveInAsync("Second", 1);
        var elsewhere = await _owner.CreatePropertyAsync("Other PG");
        await _owner.RecordPaymentAsync(first.Id, 1000m, date: "2026-10-10");
        await _owner.RecordPaymentAsync(second.Id, 2000m, date: "2026-10-11");

        var all = await _owner.PaymentsAsync();
        var forFirst = await _owner.PaymentsAsync($"tenantId={first.Id}");
        var atProperty = await _owner.PaymentsAsync($"propertyId={_room.PropertyId}");
        var atOther = await _owner.PaymentsAsync($"propertyId={elsewhere.Id}");

        Assert.Equal(["Second", "First"], all.Items.Select(p => p.TenantName)); // newest first
        Assert.Equal(1000m, Assert.Single(forFirst.Items).Amount);
        Assert.Equal((2, 0), (atProperty.TotalCount, atOther.TotalCount));
    }

    private async Task<HttpClient> StaffClientAsync(StaffPermissions permission)
    {
        var staff = await _owner.CreateStaffAsync("Staff", permission);
        var auth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        return _factory.CreateClient(auth.AccessToken);
    }
}

internal static class PaymentTestExtensions
{
    public static Task<HttpResponseMessage> RecordPaymentAsync(
        this HttpClient client, Guid tenantId, decimal amount, string date = "2026-10-12", string method = "Upi",
        string? reference = null, string? key = null, Guid[]? chargeIds = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments")
        {
            Content = JsonContent.Create(new { tenantId, amount, paymentDate = date, method, referenceNumber = reference, chargeIds }),
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> VoidPaymentAsync(this HttpClient client, Guid paymentId, string reason) =>
        client.PostAsJsonAsync($"/api/v1/payments/{paymentId}/void", new { reason });

    public static async Task<PagedResult<PaymentSummary>> PaymentsAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync(new Uri($"/api/v1/payments?pageSize=100&{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<PaymentSummary>>();
    }
}

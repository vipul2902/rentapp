using System.Net;
using System.Net.Http.Json;
using RentApp.Application.Auth;
using RentApp.Application.Common.Paging;
using RentApp.Application.Properties;
using RentApp.Domain.Properties;
using RentApp.Domain.Users;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class PropertiesTests(ContainersFixture containers) : IAsyncLifetime
{
    private RentAppFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _owner = null!;

    public async Task InitializeAsync()
    {
        _factory = RentAppFactory.For(containers);
        _anonymous = _factory.CreateClient();
        _owner = _factory.CreateClient((await _anonymous.RegisterOwnerAsync()).AccessToken);
    }

    public async Task DisposeAsync()
    {
        _owner.Dispose();
        _anonymous.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- Properties ------------------------------------------------------------------------------

    [Fact]
    public async Task OwnerCreatesUpdatesAndReadsAProperty()
    {
        var created = await _owner.CreatePropertyAsync("Sunrise PG", "Bengaluru");

        var update = await _owner.PutAsJsonAsync($"/api/v1/properties/{created.Id}", new
        {
            name = "Sunrise PG - Koramangala",
            address = "12, 5th Cross",
            city = "Bengaluru",
            state = "Karnataka",
            postalCode = "560034",
            contactPhone = "+91 98765 43210",
        });
        var fetched = await (await _owner.GetAsync(new Uri($"/api/v1/properties/{created.Id}", UriKind.Relative))).ReadAsync<PropertyDto>();

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Sunrise PG - Koramangala", fetched.Name);
        Assert.Equal("560034", fetched.PostalCode);
        Assert.Equal(PropertyStatus.Active, fetched.Status);
        Assert.Equal(OccupancySummary.Empty, fetched.Occupancy);
    }

    [Fact]
    public async Task PropertyValidationReturnsFieldErrors()
    {
        var response = await _owner.PostAsJsonAsync("/api/v1/properties", new { name = "", address = "", city = "", postalCode = "!!" });
        var error = await response.ReadErrorAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", error.Code);
        Assert.Contains("name", error.Errors!.Keys);
        Assert.Contains("city", error.Errors.Keys);
        Assert.Contains("postalCode", error.Errors.Keys);
    }

    [Fact]
    public async Task ArchivedPropertiesAreHiddenUntilRestored()
    {
        var keep = await _owner.CreatePropertyAsync("Keep PG");
        var archive = await _owner.CreatePropertyAsync("Old PG");

        var deleted = await _owner.DeleteAsync(new Uri($"/api/v1/properties/{archive.Id}", UriKind.Relative));
        var active = await _owner.ListPropertiesAsync();
        var all = await _owner.ListPropertiesAsync("includeArchived=true");
        var editArchived = await _owner.PutAsJsonAsync($"/api/v1/properties/{archive.Id}", new { name = "X PG", address = "a", city = "b" });
        var restored = await (await _owner.PostAsync(new Uri($"/api/v1/properties/{archive.Id}/restore", UriKind.Relative), null)).ReadAsync<PropertyDto>();

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Contains(active.Items, p => p.Id == keep.Id);
        Assert.DoesNotContain(active.Items, p => p.Id == archive.Id);
        Assert.Contains(all.Items, p => p.Id == archive.Id && p.Status == PropertyStatus.Archived);
        Assert.Equal("PROPERTY_ARCHIVED", (await editArchived.ReadErrorAsync()).Code);
        Assert.Equal(PropertyStatus.Active, restored.Status);
    }

    [Fact]
    public async Task ListSupportsSearchSortAndPaging()
    {
        await _owner.CreatePropertyAsync("Zen Stay", "Pune");
        await _owner.CreatePropertyAsync("Alpha Homes", "Mumbai");
        await _owner.CreatePropertyAsync("Mango PG", "Pune");

        var byName = await _owner.ListPropertiesAsync("pageSize=2");
        var byCity = await _owner.ListPropertiesAsync("sort=City");
        var search = await _owner.ListPropertiesAsync("search=pune");

        Assert.Equal(["Alpha Homes", "Mango PG"], byName.Items.Select(p => p.Name));
        Assert.Equal(3, byName.TotalCount);
        Assert.Equal(2, byName.TotalPages);
        Assert.Equal("Mumbai", byCity.Items[0].City);
        Assert.Equal(["Mango PG", "Zen Stay"], search.Items.Select(p => p.Name));
    }

    // ---- Rooms and beds --------------------------------------------------------------------------

    [Fact]
    public async Task CreatingARoomCreatesItsBedsWithDefaultRent()
    {
        var property = await _owner.CreatePropertyAsync();

        var room = await _owner.CreateRoomAsync(property.Id, "201", capacity: 3, rent: 8500m);

        Assert.Equal(["A", "B", "C"], room.Beds.Select(b => b.Label));
        Assert.All(room.Beds, b => Assert.Equal(8500m, b.DefaultMonthlyRent));
        Assert.All(room.Beds, b => Assert.Equal(BedOccupancy.Vacant, b.Occupancy));
        Assert.Equal(new OccupancySummary(3, 0, 3, 0, 0), room.Occupancy);

        var summary = await (await _owner.GetAsync(new Uri($"/api/v1/properties/{property.Id}", UriKind.Relative))).ReadAsync<PropertyDto>();
        Assert.Equal(1, summary.RoomCount);
        Assert.Equal(3, summary.Occupancy.TotalBeds);
    }

    [Fact]
    public async Task RoomNumbersAreUniquePerPropertyUntilArchived()
    {
        var property = await _owner.CreatePropertyAsync();
        var first = await _owner.CreateRoomAsync(property.Id, "101", capacity: 1);

        var duplicate = await _owner.PostAsJsonAsync($"/api/v1/properties/{property.Id}/rooms", new { roomNumber = " 101 ", capacity = 2 });
        await _owner.DeleteAsync(new Uri($"/api/v1/rooms/{first.Id}", UriKind.Relative));
        var reused = await _owner.PostAsJsonAsync($"/api/v1/properties/{property.Id}/rooms", new { roomNumber = "101", capacity = 2 });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("ROOM_NUMBER_TAKEN", (await duplicate.ReadErrorAsync()).Code);
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);
    }

    [Fact]
    public async Task OccupancyReflectsBedAndRoomStatus()
    {
        var property = await _owner.CreatePropertyAsync();
        var room = await _owner.CreateRoomAsync(property.Id, "301", capacity: 3);
        var otherRoom = await _owner.CreateRoomAsync(property.Id, "302", capacity: 2);

        await _owner.PutAsJsonAsync($"/api/v1/beds/{room.Beds[0].Id}", new { label = "A", status = "Reserved" });
        await _owner.PutAsJsonAsync($"/api/v1/beds/{room.Beds[1].Id}", new { label = "B", status = "Unavailable" });
        await _owner.PutAsJsonAsync($"/api/v1/rooms/{otherRoom.Id}", new { roomNumber = "302", capacity = 2, status = "Unavailable" });

        var rooms = await (await _owner.GetAsync(new Uri($"/api/v1/properties/{property.Id}/rooms", UriKind.Relative))).ReadAsync<List<RoomDto>>();
        var summary = await (await _owner.GetAsync(new Uri($"/api/v1/properties/{property.Id}", UriKind.Relative))).ReadAsync<PropertyDto>();

        Assert.Equal(["301", "302"], rooms.Select(r => r.RoomNumber));
        Assert.Equal(
            [BedOccupancy.Reserved, BedOccupancy.Unavailable, BedOccupancy.Vacant],
            rooms[0].Beds.Select(b => b.Occupancy));
        Assert.All(rooms[1].Beds, b => Assert.Equal(BedOccupancy.Unavailable, b.Occupancy));
        Assert.Equal(new OccupancySummary(TotalBeds: 5, Occupied: 0, Vacant: 1, Reserved: 1, Unavailable: 3), summary.Occupancy);
    }

    [Fact]
    public async Task CapacityLimitsBedsAndCannotDropBelowThem()
    {
        var property = await _owner.CreatePropertyAsync();
        var room = await _owner.CreateRoomAsync(property.Id, "401", capacity: 2);

        var full = await _owner.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/beds", new { });
        var shrink = await _owner.PutAsJsonAsync($"/api/v1/rooms/{room.Id}", new { roomNumber = "401", capacity = 1, status = "Active" });
        var grow = await _owner.PutAsJsonAsync($"/api/v1/rooms/{room.Id}", new { roomNumber = "401", capacity = 3, status = "Active" });
        var added = await (await _owner.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/beds", new { })).ReadAsync<BedDto>();

        Assert.Equal("ROOM_FULL", (await full.ReadErrorAsync()).Code);
        Assert.Equal("CAPACITY_BELOW_BED_COUNT", (await shrink.ReadErrorAsync()).Code);
        Assert.Equal(HttpStatusCode.OK, grow.StatusCode);
        Assert.Equal("C", added.Label);
    }

    [Fact]
    public async Task ConcurrentBedCreationNeverExceedsCapacity()
    {
        var property = await _owner.CreatePropertyAsync();
        var room = await _owner.CreateRoomAsync(property.Id, "501", capacity: 3, createBeds: false);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            _owner.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/beds", new { label = $"X{i}" })));
        var beds = await (await _owner.GetAsync(new Uri($"/api/v1/rooms/{room.Id}/beds", UriKind.Relative))).ReadAsync<List<BedDto>>();

        Assert.Equal(3, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(3, beds.Count);
    }

    [Fact]
    public async Task BedLabelsAreUniquePerRoomIgnoringCase()
    {
        var property = await _owner.CreatePropertyAsync();
        var room = await _owner.CreateRoomAsync(property.Id, "601", capacity: 3, createBeds: false);

        var first = await _owner.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/beds", new { label = "window" });
        var duplicate = await _owner.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/beds", new { label = "WINDOW" });

        Assert.Equal("WINDOW", (await first.ReadAsync<BedDto>()).Label);
        Assert.Equal("BED_LABEL_TAKEN", (await duplicate.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task ArchivingARoomArchivesItsBeds()
    {
        var property = await _owner.CreatePropertyAsync();
        var room = await _owner.CreateRoomAsync(property.Id, "701", capacity: 2);

        await _owner.DeleteAsync(new Uri($"/api/v1/rooms/{room.Id}", UriKind.Relative));
        var bed = await _owner.PutAsJsonAsync($"/api/v1/beds/{room.Beds[0].Id}", new { label = "A", status = "Available" });
        var summary = await (await _owner.GetAsync(new Uri($"/api/v1/properties/{property.Id}", UriKind.Relative))).ReadAsync<PropertyDto>();
        var withArchived = await (await _owner.GetAsync(new Uri($"/api/v1/properties/{property.Id}/rooms?includeArchived=true", UriKind.Relative))).ReadAsync<List<RoomDto>>();

        Assert.Equal("BED_ARCHIVED", (await bed.ReadErrorAsync()).Code);
        Assert.Equal(0, summary.RoomCount);
        Assert.Equal(OccupancySummary.Empty, summary.Occupancy);
        Assert.Equal(RoomStatus.Archived, Assert.Single(withArchived).Status);
    }

    [Fact]
    public async Task RentMustBePositiveWithTwoDecimals()
    {
        var property = await _owner.CreatePropertyAsync();

        var negative = await _owner.PostAsJsonAsync($"/api/v1/properties/{property.Id}/rooms", new { roomNumber = "R1", capacity = 1, defaultMonthlyRent = -10 });
        var fractional = await _owner.PostAsJsonAsync($"/api/v1/properties/{property.Id}/rooms", new { roomNumber = "R2", capacity = 1, defaultMonthlyRent = 8500.555 });
        var ok = await _owner.PostAsJsonAsync($"/api/v1/properties/{property.Id}/rooms", new { roomNumber = "R3", capacity = 1, defaultMonthlyRent = 8500.55 });

        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fractional.StatusCode);
        Assert.Equal(8500.55m, (await ok.ReadAsync<RoomDto>()).Beds[0].DefaultMonthlyRent);
    }

    // ---- Permissions and isolation ---------------------------------------------------------------

    [Fact]
    public async Task StaffNeedViewPropertiesToReadAndCanNeverWrite()
    {
        var property = await _owner.CreatePropertyAsync();
        using var viewer = await StaffClientAsync(StaffPermissions.ViewProperties);
        using var noAccess = await StaffClientAsync(StaffPermissions.RecordPayments);

        var viewerList = await viewer.GetAsync(new Uri("/api/v1/properties", UriKind.Relative));
        var viewerRooms = await viewer.GetAsync(new Uri($"/api/v1/properties/{property.Id}/rooms", UriKind.Relative));
        var viewerCreate = await viewer.PostAsJsonAsync("/api/v1/properties", new { name = "Nope PG", address = "a", city = "b" });
        var viewerRoom = await viewer.PostAsJsonAsync($"/api/v1/properties/{property.Id}/rooms", new { roomNumber = "9", capacity = 1 });
        var blockedList = await noAccess.GetAsync(new Uri("/api/v1/properties", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, viewerList.StatusCode);
        Assert.Equal(HttpStatusCode.OK, viewerRooms.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerRoom.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, blockedList.StatusCode);
    }

    [Fact]
    public async Task AnotherOrganizationCannotSeeOrChangeAnything()
    {
        var property = await _owner.CreatePropertyAsync();
        var room = await _owner.CreateRoomAsync(property.Id, "801", capacity: 1);
        var bed = room.Beds[0];
        using var other = _factory.CreateClient((await _anonymous.RegisterOwnerAsync("Rival PG")).AccessToken);

        var responses = new[]
        {
            await other.GetAsync(new Uri($"/api/v1/properties/{property.Id}", UriKind.Relative)),
            await other.PutAsJsonAsync($"/api/v1/properties/{property.Id}", new { name = "Hacked PG", address = "a", city = "b" }),
            await other.DeleteAsync(new Uri($"/api/v1/properties/{property.Id}", UriKind.Relative)),
            await other.GetAsync(new Uri($"/api/v1/properties/{property.Id}/rooms", UriKind.Relative)),
            await other.PostAsJsonAsync($"/api/v1/properties/{property.Id}/rooms", new { roomNumber = "X", capacity = 1 }),
            await other.GetAsync(new Uri($"/api/v1/rooms/{room.Id}", UriKind.Relative)),
            await other.PutAsJsonAsync($"/api/v1/rooms/{room.Id}", new { roomNumber = "X", capacity = 5, status = "Active" }),
            await other.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/beds", new { }),
            await other.DeleteAsync(new Uri($"/api/v1/rooms/{room.Id}", UriKind.Relative)),
            await other.GetAsync(new Uri($"/api/v1/beds/{bed.Id}", UriKind.Relative)),
            await other.PutAsJsonAsync($"/api/v1/beds/{bed.Id}", new { label = "Z", status = "Unavailable" }),
            await other.DeleteAsync(new Uri($"/api/v1/beds/{bed.Id}", UriKind.Relative)),
        };
        var otherList = await other.ListPropertiesAsync("includeArchived=true");

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Empty(otherList.Items);

        var unchanged = await (await _owner.GetAsync(new Uri($"/api/v1/rooms/{room.Id}", UriKind.Relative))).ReadAsync<RoomDto>();
        Assert.Equal("801", unchanged.RoomNumber);
        Assert.Equal(1, unchanged.Capacity);
        Assert.Equal("A", Assert.Single(unchanged.Beds).Label);
    }

    private async Task<HttpClient> StaffClientAsync(params StaffPermissions[] permissions)
    {
        var staff = await _owner.CreateStaffAsync("Staff", permissions);
        var auth = await (await _anonymous.LoginAsync(staff.Email, StaffPassword)).ReadAsync<AuthResponse>();
        return _factory.CreateClient(auth.AccessToken);
    }
}

internal static class PropertyTestExtensions
{
    public static async Task<PropertyDto> CreatePropertyAsync(this HttpClient owner, string name = "Test PG", string city = "Bengaluru")
    {
        var response = await owner.PostAsJsonAsync("/api/v1/properties", new { name, address = "1 Main Road", city });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<PropertyDto>();
    }

    public static async Task<RoomDto> CreateRoomAsync(
        this HttpClient owner, Guid propertyId, string roomNumber, int capacity, decimal? rent = null, bool createBeds = true)
    {
        var response = await owner.PostAsJsonAsync($"/api/v1/properties/{propertyId}/rooms", new
        {
            roomNumber,
            capacity,
            createBeds,
            defaultMonthlyRent = rent,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<RoomDto>();
    }

    public static async Task<PagedResult<PropertyDto>> ListPropertiesAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync(new Uri($"/api/v1/properties?{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<PropertyDto>>();
    }
}

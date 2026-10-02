using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RentApp.Application.Auth;
using RentApp.Application.Users;
using RentApp.Domain.Users;

namespace RentApp.IntegrationTests.Infrastructure;

internal static class ApiTestExtensions
{
    public const string OwnerPassword = "Owner-Pass-123";
    public const string StaffPassword = "Staff-Pass-123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";

    public static async Task<AuthResponse> RegisterOwnerAsync(this HttpClient client, string organizationName = "Sunrise PG")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            organizationName,
            name = "Asha Owner",
            email = UniqueEmail("owner"),
            password = OwnerPassword,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<AuthResponse>();
    }

    public static async Task<StaffMember> CreateStaffAsync(
        this HttpClient ownerClient, string name = "Ravi Staff", params StaffPermissions[] permissions)
    {
        var response = await ownerClient.PostAsJsonAsync("/api/v1/users", new
        {
            name,
            email = UniqueEmail("staff"),
            password = StaffPassword,
            permissions = permissions.Select(p => p.ToString()),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<StaffMember>();
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    public static HttpClient CreateClient(this RentAppFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static Task<ErrorBody> ReadErrorAsync(this HttpResponseMessage response) => response.ReadAsync<ErrorBody>();

    internal sealed record ErrorBody(string Code, string Message, string TraceId, Dictionary<string, string[]>? Errors);
}

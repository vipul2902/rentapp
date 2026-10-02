using System.Net;
using System.Net.Http.Json;
using RentApp.Application.Auth;
using RentApp.Domain.Users;
using RentApp.IntegrationTests.Infrastructure;
using static RentApp.IntegrationTests.Infrastructure.ApiTestExtensions;

namespace RentApp.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class AuthTests(ContainersFixture containers) : IAsyncLifetime
{
    private RentAppFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = RentAppFactory.For(containers);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RegisterCreatesOrganizationWithOwnerAndSignsIn()
    {
        var auth = await _client.RegisterOwnerAsync("Green Valley PG");

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.True(auth.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(UserRole.Owner, auth.User.Role);
        Assert.Equal("Green Valley PG", auth.User.Organization.Name);
        Assert.Equal("Asia/Kolkata", auth.User.Organization.TimeZone);
        Assert.Equal(StaffPermissions.All.ToList(), auth.User.Permissions);

        using var authed = _factory.CreateClient(auth.AccessToken);
        var me = await (await authed.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative))).ReadAsync<UserProfile>();
        Assert.Equal(auth.User.Id, me.Id);
        Assert.Equal(auth.User.Organization.Id, me.Organization.Id);
    }

    [Fact]
    public async Task RegisterRejectsDuplicateEmailIgnoringCase()
    {
        var email = UniqueEmail("dup");
        var first = await _client.PostAsJsonAsync("/api/v1/auth/register", new { organizationName = "One PG", name = "First", email, password = OwnerPassword });
        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", new { organizationName = "Two PG", name = "Second", email = email.ToUpperInvariant(), password = OwnerPassword });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("EMAIL_ALREADY_REGISTERED", (await second.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task RegisterReturnsFieldErrorsUsingJsonNames()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { organizationName = "", name = "A", email = "not-an-email", password = "short" });
        var error = await response.ReadErrorAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", error.Code);
        Assert.Contains("password", error.Errors!.Keys);
        Assert.Contains("email", error.Errors.Keys);
        Assert.Contains("organizationName", error.Errors.Keys);
        Assert.Contains("Password must be 8 to 128 characters.", error.Errors["password"]);
    }

    [Fact]
    public async Task LoginWorksWithDifferentEmailCase()
    {
        var auth = await _client.RegisterOwnerAsync();

        var response = await _client.LoginAsync($"  {auth.User.Email.ToUpperInvariant()} ", OwnerPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(auth.User.Id, (await response.ReadAsync<AuthResponse>()).User.Id);
    }

    [Fact]
    public async Task WrongPasswordAndUnknownEmailAreIndistinguishable()
    {
        var auth = await _client.RegisterOwnerAsync();

        var wrongPassword = await _client.LoginAsync(auth.User.Email, "Wrong-Pass-123");
        var unknownEmail = await _client.LoginAsync(UniqueEmail("nobody"), "Wrong-Pass-123");
        var a = await wrongPassword.ReadErrorAsync();
        var b = await unknownEmail.ReadErrorAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", a.Code);
        Assert.Equal((a.Code, a.Message), (b.Code, b.Message));
    }

    [Fact]
    public async Task ProtectedEndpointsRequireAToken()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await response.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task TamperedTokenIsRejected()
    {
        var auth = await _client.RegisterOwnerAsync();
        var tampered = auth.AccessToken[..^4] + (auth.AccessToken.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        using var client = _factory.CreateClient(tampered);
        var response = await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshRotatesTokensAndReuseRevokesTheWholeSession()
    {
        var auth = await _client.RegisterOwnerAsync();

        var first = await _client.RefreshAsync(auth.RefreshToken);
        var rotated = await first.ReadAsync<AuthResponse>();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);

        // Replaying the old token signals theft: it fails and kills the newer token too.
        var replay = await _client.RefreshAsync(auth.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("SESSION_EXPIRED", (await replay.ReadErrorAsync()).Code);

        var afterReuse = await _client.RefreshAsync(rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefreshWithOneTokenSucceedsAtMostOnce()
    {
        var auth = await _client.RegisterOwnerAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.RefreshAsync(auth.RefreshToken)));

        Assert.True(results.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutEndsTheSession()
    {
        var auth = await _client.RegisterOwnerAsync();

        var logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = auth.RefreshToken });
        var refresh = await _client.RefreshAsync(auth.RefreshToken);
        var unknown = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
    }

    [Fact]
    public async Task OpenApiIsNotExposedOutsideDevelopment()
    {
        var auth = await _client.RegisterOwnerAsync();
        using var authed = _factory.CreateClient(auth.AccessToken);

        var anonymous = await _client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        var signedIn = await authed.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, signedIn.StatusCode);
    }

    [Fact]
    public async Task SignedInRequestToUnknownRouteIsNotFound()
    {
        var auth = await _client.RegisterOwnerAsync();
        using var authed = _factory.CreateClient(auth.AccessToken);

        var response = await authed.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", (await response.ReadErrorAsync()).Code);
    }
}

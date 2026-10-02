using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RentApp.Application.Common.Security;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Security;

namespace RentApp.UnitTests.Security;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Secret = "unit-test-secret-that-is-long-enough-0123456789",
        Issuer = "issuer",
        Audience = "audience",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30,
    };

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));

    private JwtTokenService Service => new(Microsoft.Extensions.Options.Options.Create(Options), _clock);

    [Fact]
    public async Task AccessTokenCarriesIdentityOrganizationRoleAndPermissions()
    {
        var staff = User.CreateStaff(Guid.NewGuid(), "Ravi", "r@example.com", null, "hash",
            StaffPermissions.RecordPayments | StaffPermissions.ViewTenants);

        var token = Service.CreateAccessToken(staff);
        var result = await Validate(token.Value);

        Assert.True(result.IsValid);
        Assert.Equal(staff.Id.ToString(), result.Claims[AppClaimTypes.Subject]);
        Assert.Equal(staff.OrganizationId.ToString(), result.Claims[AppClaimTypes.Organization]);
        Assert.Equal("Staff", result.Claims[AppClaimTypes.Role]);
        var permissions = new JsonWebToken(token.Value).Claims.Where(c => c.Type == AppClaimTypes.Permission).Select(c => c.Value);
        Assert.Equal(["ViewTenants", "RecordPayments"], permissions);
        Assert.Equal(_clock.GetUtcNow().AddMinutes(15), token.ExpiresAt);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKeyIsRejected()
    {
        var token = Service.CreateAccessToken(User.CreateOwner(Guid.NewGuid(), "Asha", "a@example.com", null, "hash"));

        var result = await Validate(token.Value, key: "a-completely-different-secret-0123456789-xyz");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RefreshTokensAreRandomAndOnlyTheHashIsDerivable()
    {
        var a = Service.CreateRefreshToken();
        var b = Service.CreateRefreshToken();

        Assert.NotEqual(a.Value, b.Value);
        Assert.Equal(64, a.Hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", a.Hash);
        Assert.Equal(a.Hash, Service.HashRefreshToken(a.Value));
        Assert.Equal(_clock.GetUtcNow().AddDays(30), a.ExpiresAt);
    }

    [Theory]
    [InlineData("too-short", "issuer", "audience", false)]
    [InlineData("unit-test-secret-that-is-long-enough-0123456789", "", "audience", false)]
    [InlineData("unit-test-secret-that-is-long-enough-0123456789", "issuer", "", false)]
    [InlineData("unit-test-secret-that-is-long-enough-0123456789", "issuer", "audience", true)]
    public void OptionsValidationRequiresStrongSecretAndIssuerAudience(string secret, string issuer, string audience, bool valid)
    {
        Assert.Equal(valid, JwtOptions.IsValid(new JwtOptions { Secret = secret, Issuer = issuer, Audience = audience }));
    }

    private static Task<TokenValidationResult> Validate(string token, string? key = null) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key ?? Options.Secret)),
            ValidateLifetime = false,
        });
}

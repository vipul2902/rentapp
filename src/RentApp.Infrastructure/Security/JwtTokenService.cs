using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RentApp.Application.Common.Security;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Security;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private const int RefreshTokenBytes = 32;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaimTypes.Subject, user.Id.ToString()),
            new(AppClaimTypes.Organization, user.OrganizationId.ToString()),
            new(AppClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(user.Permissions.ToList().Select(p => new Claim(AppClaimTypes.Permission, p.ToString())));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(jwt.SecretBytes), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }

    public NewRefreshToken CreateRefreshToken()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        var expires = clock.GetUtcNow().AddDays(options.Value.RefreshTokenDays);
        return new NewRefreshToken(value, HashRefreshToken(value), expires);
    }

    /// <summary>SHA-256 is appropriate here: the token is 256 random bits, so it cannot be brute-forced.</summary>
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

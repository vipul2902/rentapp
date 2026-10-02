using RentApp.Domain.Users;

namespace RentApp.Application.Common.Security;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>A new random refresh token. Only <see cref="NewRefreshToken.Hash"/> may be stored.</summary>
    NewRefreshToken CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record NewRefreshToken(string Value, string Hash, DateTimeOffset ExpiresAt);

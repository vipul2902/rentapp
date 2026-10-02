using System.Text;

namespace RentApp.Infrastructure.Security;

/// <summary>Bound from the <c>Jwt</c> section (JWT_SECRET, JWT_ISSUER, JWT_AUDIENCE).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretBytes = 32;

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Short-lived by design: permission changes and disabled accounts take effect within this window.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;

    public byte[] SecretBytes => Encoding.UTF8.GetBytes(Secret);

    internal static bool IsValid(JwtOptions o) =>
        Encoding.UTF8.GetByteCount(o.Secret) >= MinimumSecretBytes
        && !string.IsNullOrWhiteSpace(o.Issuer)
        && !string.IsNullOrWhiteSpace(o.Audience)
        && o.AccessTokenMinutes is >= 1 and <= 60
        && o.RefreshTokenDays is >= 1 and <= 90;
}

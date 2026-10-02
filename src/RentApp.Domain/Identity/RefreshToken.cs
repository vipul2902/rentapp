using RentApp.Domain.Common;

namespace RentApp.Domain.Identity;

/// <summary>
/// A refresh token. Only a SHA-256 hash is stored. Tokens rotate on every use; all tokens descending from
/// one login share a <see cref="FamilyId"/>, so presenting an already-rotated token revokes the whole family.
/// </summary>
public sealed class RefreshToken : Entity, IOrganizationScoped
{
    public const int HashLength = 64;

    private RefreshToken()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshTokenRevocation? RevokedReason { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Issue(
        Guid organizationId, Guid userId, Guid familyId, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt) => new()
    {
        OrganizationId = organizationId,
        UserId = userId,
        FamilyId = familyId,
        TokenHash = tokenHash,
        CreatedAt = now,
        ExpiresAt = expiresAt,
    };
}

public enum RefreshTokenRevocation
{
    Rotated = 1,
    LoggedOut = 2,
    ReuseDetected = 3,
    UserDisabled = 4,
    PasswordReset = 5,
}

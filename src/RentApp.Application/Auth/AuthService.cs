using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Security;
using RentApp.Domain.Identity;
using RentApp.Domain.Organizations;
using RentApp.Domain.Users;

namespace RentApp.Application.Auth;

/// <summary>
/// Sign-up, sign-in, token refresh and sign-out. These flows run before a caller is authenticated, which is
/// why they — and only they — query with IgnoreQueryFilters().
/// </summary>
public sealed partial class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    AuditWriter audit,
    ICurrentUser currentUser,
    TimeProvider clock,
    ILogger<AuthService> logger)
{
    // Verified against when the email is unknown, so both paths cost one hash and timing does not leak accounts.
    private static string? s_dummyHash;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw AuthErrors.EmailTaken();
        }

        var organization = Organization.Create(request.OrganizationName);
        var owner = User.CreateOwner(organization.Id, request.Name, request.Email, request.Phone, passwordHasher.Hash(request.Password));
        owner.RecordLogin(clock.GetUtcNow());

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Organizations.Add(organization);
        db.Users.Add(owner);
        await SaveOrThrowEmailTakenAsync(cancellationToken);

        // Organization and owner reference each other; the owner link is set once both rows exist.
        organization.AssignOwner(owner.Id);
        audit.Record(organization.Id, owner.Id, AuditActions.OrganizationRegistered, nameof(Organization), organization.Id);

        var response = await IssueTokensAsync(owner, organization, Guid.CreateVersion7(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            passwordHasher.Verify(s_dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString()), request.Password);
            throw AuthErrors.InvalidCredentials();
        }

        var check = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (check == PasswordCheck.Failed)
        {
            throw AuthErrors.InvalidCredentials();
        }

        var organization = await LoadOrganizationAsync(user.OrganizationId, cancellationToken);
        if (!user.IsActive || !organization.IsActive)
        {
            throw AuthErrors.AccountDisabled();
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.Hash(request.Password));
        }

        user.RecordLogin(clock.GetUtcNow());
        return await IssueTokensAsync(user, organization, Guid.CreateVersion7(), cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var presented = await db.RefreshTokens.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (presented is null || presented.ExpiresAt <= now)
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        if (presented.RevokedAt is not null)
        {
            if (presented.RevokedReason == RefreshTokenRevocation.Rotated)
            {
                // A rotated token came back: it was copied. Kill every session descended from that login.
                await RevokeFamilyAsync(presented.FamilyId, RefreshTokenRevocation.ReuseDetected, now, cancellationToken);
                LogReuseDetected(logger, presented.UserId, presented.FamilyId);
            }

            throw AuthErrors.InvalidRefreshToken();
        }

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == presented.UserId, cancellationToken);
        var organization = await LoadOrganizationAsync(user.OrganizationId, cancellationToken);
        if (!user.IsActive || !organization.IsActive)
        {
            await RevokeFamilyAsync(presented.FamilyId, RefreshTokenRevocation.UserDisabled, now, cancellationToken);
            throw AuthErrors.AccountDisabled();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Claim the presented token atomically. If another request already rotated it, treat as reuse.
        var replacement = tokenService.CreateRefreshToken();
        var replacementEntity = RefreshToken.Issue(user.OrganizationId, user.Id, presented.FamilyId, replacement.Hash, now, replacement.ExpiresAt);
        var claimed = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.Id == presented.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, RefreshTokenRevocation.Rotated)
                .SetProperty(t => t.ReplacedByTokenId, replacementEntity.Id), cancellationToken);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            await RevokeFamilyAsync(presented.FamilyId, RefreshTokenRevocation.ReuseDetected, now, cancellationToken);
            LogReuseDetected(logger, presented.UserId, presented.FamilyId);
            throw AuthErrors.InvalidRefreshToken();
        }

        db.RefreshTokens.Add(replacementEntity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var access = tokenService.CreateAccessToken(user);
        return new AuthResponse(access.Value, access.ExpiresAt, replacement.Value, replacement.ExpiresAt, UserProfile.From(user, organization));
    }

    /// <summary>Ends the session the token belongs to. Always succeeds, so it cannot be used to probe tokens.</summary>
    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var familyId = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (familyId is not null)
        {
            await RevokeFamilyAsync(familyId.Value, RefreshTokenRevocation.LoggedOut, clock.GetUtcNow(), cancellationToken);
        }
    }

    public async Task<UserProfile> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        // Regular (filtered) queries: the caller can only ever see their own organization.
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        var organization = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (user is null || organization is null)
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        return UserProfile.From(user, organization);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, Organization organization, Guid familyId, CancellationToken cancellationToken)
    {
        var refresh = tokenService.CreateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Issue(user.OrganizationId, user.Id, familyId, refresh.Hash, clock.GetUtcNow(), refresh.ExpiresAt));
        await db.SaveChangesAsync(cancellationToken);

        var access = tokenService.CreateAccessToken(user);
        return new AuthResponse(access.Value, access.ExpiresAt, refresh.Value, refresh.ExpiresAt, UserProfile.From(user, organization));
    }

    private Task<Organization> LoadOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.Organizations.IgnoreQueryFilters().SingleAsync(o => o.Id == organizationId, cancellationToken);

    private Task<int> RevokeFamilyAsync(Guid familyId, RefreshTokenRevocation reason, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, reason), cancellationToken);

    private async Task SaveOrThrowEmailTakenAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            throw AuthErrors.EmailTaken();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; revoked token family {FamilyId}")]
    private static partial void LogReuseDetected(ILogger logger, Guid userId, Guid familyId);
}

using Microsoft.EntityFrameworkCore;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Domain.Identity;
using RentApp.Domain.Users;

namespace RentApp.Application.Users;

/// <summary>
/// Owner-only management of staff accounts. The API enforces the Owner policy; this service checks again
/// so the rule holds no matter who calls it. All queries are organization-filtered.
/// </summary>
public sealed class UserManagementService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    AuditWriter audit,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<PagedResult<StaffMember>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        EnsureOwner();
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL upper(...) LIKE; .NET culture rules do not apply.
            users = users.Where(u => u.Name.ToUpper().Contains(term) || u.NormalizedEmail.Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await users.CountAsync(cancellationToken);
        var page = await users
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Name)
            .ThenBy(u => u.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffMember>([.. page.Select(StaffMember.From)], query.Page, query.PageSize, total);
    }

    public async Task<StaffMember> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureOwner();
        return StaffMember.From(await FindAsync(userId, cancellationToken));
    }

    public async Task<StaffMember> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        EnsureOwner();
        var normalizedEmail = User.NormalizeEmail(request.Email);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw EmailTaken();
        }

        var permissions = StaffPermissionsExtensions.Combine(request.Permissions);
        var staff = User.CreateStaff(
            currentUser.OrganizationId, request.Name, request.Email, request.Phone, passwordHasher.Hash(request.Password), permissions);

        db.Users.Add(staff);
        audit.Record(AuditActions.StaffCreated, nameof(User), staff.Id, new { permissions = permissions.ToList() });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            throw EmailTaken();
        }

        return StaffMember.From(staff);
    }

    public async Task<StaffMember> UpdatePermissionsAsync(Guid userId, UpdatePermissionsRequest request, CancellationToken cancellationToken)
    {
        EnsureOwner();
        var user = await FindStaffAsync(userId, cancellationToken);
        var before = user.Permissions;
        var after = StaffPermissionsExtensions.Combine(request.Permissions);
        if (before != after)
        {
            user.SetPermissions(after);
            audit.Record(AuditActions.PermissionsChanged, nameof(User), user.Id, new { from = before.ToList(), to = after.ToList() });
            await db.SaveChangesAsync(cancellationToken);
        }

        return StaffMember.From(user);
    }

    public async Task<StaffMember> DisableAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureOwner();
        var user = await FindStaffAsync(userId, cancellationToken);
        if (user.IsActive)
        {
            user.Disable();
            audit.Record(AuditActions.UserDisabled, nameof(User), user.Id);
            await SaveAndRevokeSessionsAsync(user.Id, RefreshTokenRevocation.UserDisabled, cancellationToken);
        }

        return StaffMember.From(user);
    }

    public async Task<StaffMember> EnableAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureOwner();
        var user = await FindStaffAsync(userId, cancellationToken);
        if (!user.IsActive)
        {
            user.Enable();
            audit.Record(AuditActions.UserEnabled, nameof(User), user.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return StaffMember.From(user);
    }

    /// <summary>The V1 replacement for "forgot password": the owner sets a new password for a staff member.</summary>
    public async Task ResetPasswordAsync(Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        EnsureOwner();
        var user = await FindStaffAsync(userId, cancellationToken);
        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        audit.Record(AuditActions.PasswordReset, nameof(User), user.Id);
        await SaveAndRevokeSessionsAsync(user.Id, RefreshTokenRevocation.PasswordReset, cancellationToken);
    }

    private async Task SaveAndRevokeSessionsAsync(Guid userId, RefreshTokenRevocation reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, reason), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<User> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new NotFoundException("USER_NOT_FOUND", "The requested user was not found.");

    private async Task<User> FindStaffAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindAsync(userId, cancellationToken);
        return user.IsOwner
            ? throw new ValidationException("OWNER_CANNOT_BE_MODIFIED", "The owner account cannot be changed here.")
            : user;
    }

    private void EnsureOwner()
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsOwner)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Only the owner can manage staff.");
        }
    }

    private static ConflictException EmailTaken() =>
        new("EMAIL_ALREADY_REGISTERED", "An account with this email already exists.");
}

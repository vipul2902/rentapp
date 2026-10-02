using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RentApp.Application.Common.Security;
using RentApp.Domain.Common;

namespace RentApp.Infrastructure.Persistence;

/// <summary>
/// Defense in depth behind the query filters: an authenticated request may only insert or modify rows of
/// its own organization. A violation is a programming error, so it fails the whole save.
/// Unauthenticated saves are limited to the sign-up/sign-in flows, which create rows for a known organization.
/// </summary>
internal sealed class OrganizationIsolationInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Check(DbContext? context)
    {
        if (context is null || !currentUser.IsAuthenticated)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<IOrganizationScoped>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity.OrganizationId != currentUser.OrganizationId)
            {
                throw new InvalidOperationException(
                    $"Blocked a cross-organization write to {entry.Metadata.ClrType.Name} ({entry.State}).");
            }
        }
    }
}

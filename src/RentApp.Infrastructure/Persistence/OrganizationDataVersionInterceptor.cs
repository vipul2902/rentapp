using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RentApp.Domain.Audit;
using RentApp.Domain.Common;
using RentApp.Domain.Identity;
using RentApp.Domain.Users;
using RentApp.Infrastructure.Caching;

namespace RentApp.Infrastructure.Persistence;

/// <summary>
/// After a write to business data commits, bumps the organization's data version so cached dashboards are
/// refreshed on the next read. Inside a transaction the bump waits for the commit, so a reader can never cache
/// figures from before the change under the new version. Every write path in the app saves through
/// SaveChanges, including those that also use ExecuteUpdate in the same transaction.
/// One instance per DbContext (scoped).
/// </summary>
internal sealed class OrganizationDataVersionInterceptor(IOrganizationDataVersion version) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly HashSet<Guid> _saving = [];
    private readonly HashSet<Guid> _awaitingCommit = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await AfterSaveAsync(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        AfterSaveAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _saving.Clear();

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _saving.Clear();
        return Task.CompletedTask;
    }

    public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
        await FlushCommittedAsync();

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        FlushCommittedAsync().GetAwaiter().GetResult();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _awaitingCommit.Clear();
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => _awaitingCommit.Clear();

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Sign-ins, token refreshes, staff accounts and audit entries do not change any figure we cache.
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity is IOrganizationScoped scoped and not (RefreshToken or User or AuditLog))
            {
                _saving.Add(scoped.OrganizationId);
            }
        }
    }

    private async Task AfterSaveAsync(DbContext? context)
    {
        if (_saving.Count == 0)
        {
            return;
        }

        if (context?.Database.CurrentTransaction is not null)
        {
            _awaitingCommit.UnionWith(_saving);
            _saving.Clear();
            return;
        }

        var organizations = _saving.ToList();
        _saving.Clear();
        await version.BumpAsync(organizations);
    }

    private async Task FlushCommittedAsync()
    {
        if (_awaitingCommit.Count == 0)
        {
            return;
        }

        var organizations = _awaitingCommit.ToList();
        _awaitingCommit.Clear();
        await version.BumpAsync(organizations);
    }
}

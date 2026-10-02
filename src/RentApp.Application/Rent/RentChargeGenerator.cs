using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Security;
using RentApp.Application.Common.Time;
using RentApp.Domain.Rent;
using RentApp.Domain.Tenants;

namespace RentApp.Application.Rent;

/// <summary>
/// Materializes the charges <see cref="RentSchedule"/> says should exist, for the current organization.
/// Idempotent: existing charges (including cancelled ones) are never recreated, and the unique index on
/// (tenancy, month) makes concurrent runs safe.
/// </summary>
public sealed partial class RentChargeGenerator(
    IAppDbContext db,
    ICurrentUser currentUser,
    OrganizationClock calendar,
    TimeProvider clock,
    ILogger<RentChargeGenerator> logger)
{
    /// <summary>Creates missing charges for the given tenants (or everyone in the organization). Returns how many were created.</summary>
    public async Task<int> GenerateAsync(IReadOnlyCollection<Guid>? tenantIds, CancellationToken cancellationToken)
    {
        // One generation run per organization at a time (across API instances). Without this, concurrent
        // runs inserting the same charges in different orders can deadlock on the unique index.
        var ownTransaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            await db.AcquireOrganizationLockAsync(LockPurpose, cancellationToken);
            var created = await GenerateOnceAsync(tenantIds, cancellationToken);
            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancellationToken);
            }

            return created;
        }
        finally
        {
            if (ownTransaction is not null)
            {
                await ownTransaction.DisposeAsync();
            }
        }
    }

    private const string LockPurpose = "rent-generation";

    /// <summary>
    /// When a tenancy ends, charges for months after the last day (or all of them, for a cancelled
    /// booking) are no longer owed. Only charges with no payment are cancelled; paid ones are kept.
    /// </summary>
    public Task<int> CancelUnpaidAfterAsync(Guid agreementId, DateOnly? lastDay, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return db.RentCharges
            .Where(c => c.RentAgreementId == agreementId && c.CancelledAt == null && c.PaidAmount == 0
                        && (lastDay == null || c.PeriodStart > lastDay))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CancelledAt, now).SetProperty(c => c.UpdatedAt, now), cancellationToken);
    }

    private async Task<int> GenerateOnceAsync(IReadOnlyCollection<Guid>? tenantIds, CancellationToken cancellationToken)
    {
        var today = await calendar.TodayAsync(cancellationToken);

        var agreements = await db.RentAgreements.AsNoTracking()
            .Where(a => tenantIds == null || tenantIds.Contains(a.TenantId))
            .ToListAsync(cancellationToken);
        if (agreements.Count == 0)
        {
            return 0;
        }

        var agreementIds = agreements.Select(a => a.Id).ToList();
        var existing = (await db.RentCharges.AsNoTracking()
                .Where(c => agreementIds.Contains(c.RentAgreementId))
                .Select(c => new { c.RentAgreementId, c.PeriodStart })
                .ToListAsync(cancellationToken))
            .Select(c => (c.RentAgreementId, c.PeriodStart))
            .ToHashSet();

        var byId = agreements.ToDictionary(a => a.Id);
        var created = 0;
        foreach (var tenantAgreements in agreements.GroupBy(a => a.TenantId))
        {
            var schedule = RentSchedule.ChargesFor([.. tenantAgreements.Select(RentStatusRules.TermsOf)], today);
            foreach (var due in schedule.Where(s => !existing.Contains((s.AgreementId, s.PeriodStart))))
            {
                var agreement = byId[due.AgreementId];
                db.RentCharges.Add(RentCharge.Create(currentUser.OrganizationId, agreement.Id, agreement.TenantId, agreement.PropertyId, due));
                created++;
            }
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogGenerated(logger, created, currentUser.OrganizationId);
        }

        return created;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} rent charges for organization {OrganizationId}")]
    private static partial void LogGenerated(ILogger logger, int count, Guid organizationId);
}

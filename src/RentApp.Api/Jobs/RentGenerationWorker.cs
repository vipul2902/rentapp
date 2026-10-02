using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentApp.Application.Common.Security;
using RentApp.Application.Rent;
using RentApp.Domain.Organizations;
using RentApp.Domain.Tenants;
using RentApp.Infrastructure.Persistence;

namespace RentApp.Api.Jobs;

public sealed class RentGenerationOptions
{
    public const string SectionName = "RentGeneration";

    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 360;
}

/// <summary>
/// Keeps every organization's rent charges up to date (new month's dues appear a week ahead). Each
/// organization runs in its own scope as <see cref="SystemCurrentUser"/>, so isolation filters and the
/// cross-organization write guard apply exactly as for a person. Safe to run on several instances at
/// once: generation is idempotent and protected by a unique index.
/// </summary>
internal sealed partial class RentGenerationWorker(
    IServiceScopeFactory scopes, IOptions<RentGenerationOptions> options, ILogger<RentGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass over all active organizations with active tenancies. Returns charges created.</summary>
    internal async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        List<Guid> organizationIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            organizationIds = await db.Organizations.IgnoreQueryFilters()
                .Where(o => o.Status == OrganizationStatus.Active
                            && db.RentAgreements.IgnoreQueryFilters().Any(a => a.OrganizationId == o.Id && a.Status == AgreementStatus.Active))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
        }

        var total = 0;
        foreach (var organizationId in organizationIds)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<CurrentUserOverride>().User = new SystemCurrentUser(organizationId);
                var activeTenants = await scope.ServiceProvider.GetRequiredService<AppDbContext>().RentAgreements
                    .Where(a => a.Status == AgreementStatus.Active)
                    .Select(a => a.TenantId)
                    .ToListAsync(cancellationToken);
                total += await scope.ServiceProvider.GetRequiredService<RentChargeGenerator>().GenerateAsync(activeTenants, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One organization's problem must not stop the others.
                LogOrganizationFailed(logger, ex, organizationId);
            }
        }

        return total;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Rent generation failed for organization {OrganizationId}")]
    private static partial void LogOrganizationFailed(ILogger logger, Exception exception, Guid organizationId);
}

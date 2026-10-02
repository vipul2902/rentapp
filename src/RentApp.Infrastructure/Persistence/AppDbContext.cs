using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Security;
using RentApp.Domain.Audit;
using RentApp.Domain.Common;
using RentApp.Domain.Identity;
using RentApp.Domain.Organizations;
using RentApp.Domain.Properties;
using RentApp.Domain.Rent;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : DbContext(options), IAppDbContext
{
    private static readonly MethodInfo ApplyOrganizationFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyOrganizationFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Property> Properties => Set<Property>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Bed> Beds => Set<Bed>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<RentAgreement> RentAgreements => Set<RentAgreement>();

    public DbSet<RentCharge> RentCharges => Set<RentCharge>();

    public DbSet<RentChargeAdjustment> RentChargeAdjustments => Set<RentChargeAdjustment>();

    /// <summary>
    /// Evaluated per query (EF parameterizes context members in filters). Unauthenticated callers get
    /// Guid.Empty, which matches no rows: isolation fails closed.
    /// </summary>
    private Guid CurrentOrganizationId => currentUser.IsAuthenticated ? currentUser.OrganizationId : Guid.Empty;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

    public Task AcquireOrganizationLockAsync(string purpose, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An organization lock must be taken inside a transaction.");
        }

        var key = $"{purpose}:{CurrentOrganizationId}";
        return Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Organization>().HasQueryFilter(o => o.Id == CurrentOrganizationId);

        // Every organization-scoped entity gets the tenant filter automatically, so a new entity cannot
        // be added without isolation by forgetting a line here.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IOrganizationScoped).IsAssignableFrom(t.ClrType)))
        {
            ApplyOrganizationFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money is always numeric(12,2), never floating point. Individual properties may override.
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }

    private void ApplyOrganizationFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IOrganizationScoped =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.OrganizationId == CurrentOrganizationId);
}

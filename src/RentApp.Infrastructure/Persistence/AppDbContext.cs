using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Security;
using RentApp.Domain.Audit;
using RentApp.Domain.Common;
using RentApp.Domain.Identity;
using RentApp.Domain.Organizations;
using RentApp.Domain.Payments;
using RentApp.Domain.Properties;
using RentApp.Domain.Reminders;
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

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

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

    public async Task<long> NextReceiptSequenceAsync(int year, CancellationToken cancellationToken)
    {
        var transaction = Database.CurrentTransaction
                          ?? throw new InvalidOperationException("Receipt numbers must be taken inside a transaction.");
        if (!currentUser.IsAuthenticated)
        {
            throw new InvalidOperationException("Receipt numbers need an organization.");
        }

        // Upsert-and-increment in one statement: the row lock it takes is held until commit, so concurrent
        // payments queue for the next number, and a rollback gives the number back.
        await using var command = Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            INSERT INTO receipt_counters (organization_id, year, last_number) VALUES (@organization_id, @year, 1)
            ON CONFLICT (organization_id, year) DO UPDATE SET last_number = receipt_counters.last_number + 1
            RETURNING last_number
            """;
        command.Parameters.Add(new NpgsqlParameter("organization_id", currentUser.OrganizationId));
        command.Parameters.Add(new NpgsqlParameter("year", year));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
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

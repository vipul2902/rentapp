using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using RentApp.Domain.Audit;
using RentApp.Domain.Identity;
using RentApp.Domain.Organizations;
using RentApp.Domain.Properties;
using RentApp.Domain.Rent;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Application.Common.Abstractions;

/// <summary>
/// The application's view of the database. Organization-scoped sets are already filtered to the caller's
/// organization; only authentication flows (which run before a caller exists) may use IgnoreQueryFilters().
/// SaveChangesAsync throws <see cref="Errors.UniqueConstraintViolationException"/> on duplicate keys.
/// </summary>
public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Property> Properties { get; }

    DbSet<Room> Rooms { get; }

    DbSet<Bed> Beds { get; }

    DbSet<Tenant> Tenants { get; }

    DbSet<RentAgreement> RentAgreements { get; }

    DbSet<RentCharge> RentCharges { get; }

    DbSet<RentChargeAdjustment> RentChargeAdjustments { get; }

    DatabaseFacade Database { get; }

    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes a job per organization: takes a database lock held until the current transaction ends.
    /// Works across API instances. Must be called inside a transaction.
    /// </summary>
    Task AcquireOrganizationLockAsync(string purpose, CancellationToken cancellationToken);
}

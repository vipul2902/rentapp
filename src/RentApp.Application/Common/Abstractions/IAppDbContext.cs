using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using RentApp.Domain.Audit;
using RentApp.Domain.Identity;
using RentApp.Domain.Organizations;
using RentApp.Domain.Properties;
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

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

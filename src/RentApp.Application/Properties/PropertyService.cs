using Microsoft.EntityFrameworkCore;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Domain.Properties;
using RentApp.Domain.Users;

namespace RentApp.Application.Properties;

/// <summary>Properties (PGs/buildings). Viewing needs ViewProperties; changes are owner-only.</summary>
public sealed class PropertyService(IAppDbContext db, AuditWriter audit, ICurrentUser currentUser)
{
    public async Task<PagedResult<PropertyDto>> ListAsync(PropertyListQuery query, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewProperties);

        var properties = db.Properties.AsNoTracking();
        if (!query.IncludeArchived)
        {
            properties = properties.Where(p => p.Status == PropertyStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL upper(...) LIKE; .NET culture rules do not apply.
            properties = properties.Where(p => p.Name.ToUpper().Contains(term) || p.City.ToUpper().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await properties.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            PropertySort.City => properties.OrderBy(p => p.City).ThenBy(p => p.Name),
            PropertySort.Newest => properties.OrderByDescending(p => p.CreatedAt),
            _ => properties.OrderBy(p => p.Name),
        };
        var page = await ordered.ThenBy(p => p.Id).Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);

        var stats = await OccupancyQueries.ForPropertiesAsync(db, [.. page.Select(p => p.Id)], cancellationToken);
        var items = page.Select(p => PropertyDto.From(p, stats[p.Id].RoomCount, stats[p.Id].Occupancy)).ToList();
        return new PagedResult<PropertyDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PropertyDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewProperties);
        var property = await FindAsync(id, cancellationToken);
        return await ToDtoAsync(property, cancellationToken);
    }

    public async Task<PropertyDto> CreateAsync(PropertyRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var property = Property.Create(
            currentUser.OrganizationId, request.Name, request.Address, request.City, request.State, request.PostalCode, request.ContactPhone);

        db.Properties.Add(property);
        audit.Record(PropertyAuditActions.PropertyCreated, nameof(Property), property.Id, new { property.Name });
        await db.SaveChangesAsync(cancellationToken);
        return PropertyDto.From(property, 0, OccupancySummary.Empty);
    }

    public async Task<PropertyDto> UpdateAsync(Guid id, PropertyRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var property = await FindAsync(id, cancellationToken);
        if (property.IsArchived)
        {
            throw PropertyErrors.PropertyArchived();
        }

        property.Update(request.Name, request.Address, request.City, request.State, request.PostalCode, request.ContactPhone);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(property, cancellationToken);
    }

    /// <summary>Hides the property from lists. Rooms and beds are kept as they are, so restoring brings everything back.</summary>
    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var property = await FindAsync(id, cancellationToken);
        if (!property.IsArchived)
        {
            // Phase 4: refuse while the property has active tenancies.
            property.Archive();
            audit.Record(PropertyAuditActions.PropertyArchived, nameof(Property), property.Id);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PropertyDto> RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var property = await FindAsync(id, cancellationToken);
        if (property.IsArchived)
        {
            property.Restore();
            audit.Record(PropertyAuditActions.PropertyRestored, nameof(Property), property.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await ToDtoAsync(property, cancellationToken);
    }

    private async Task<Property> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Properties.SingleOrDefaultAsync(p => p.Id == id, cancellationToken) ?? throw PropertyErrors.PropertyNotFound();

    private async Task<PropertyDto> ToDtoAsync(Property property, CancellationToken cancellationToken)
    {
        var stats = (await OccupancyQueries.ForPropertiesAsync(db, [property.Id], cancellationToken))[property.Id];
        return PropertyDto.From(property, stats.RoomCount, stats.Occupancy);
    }
}

public static class PropertyAuditActions
{
    public const string PropertyCreated = "property.created";
    public const string PropertyArchived = "property.archived";
    public const string PropertyRestored = "property.restored";
    public const string RoomCreated = "room.created";
    public const string RoomArchived = "room.archived";
    public const string BedCreated = "bed.created";
    public const string BedArchived = "bed.archived";
}

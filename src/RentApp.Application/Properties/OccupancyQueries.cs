using Microsoft.EntityFrameworkCore;
using RentApp.Application.Common.Abstractions;
using RentApp.Domain.Properties;
using RentApp.Domain.Tenants;

namespace RentApp.Application.Properties;

/// <summary>
/// Occupancy is computed from bed status, room status and active tenancies at read time, never stored,
/// so it cannot drift. Aggregation happens in SQL; only grouped counts come back.
/// </summary>
internal static class OccupancyQueries
{
    internal sealed record PropertyStats(int RoomCount, OccupancySummary Occupancy);

    /// <summary>A bed's active tenancy, if any.</summary>
    internal sealed record BedTenancy(TenancyState State, BedTenant? Tenant);

    public static async Task<IReadOnlyDictionary<Guid, PropertyStats>> ForPropertiesAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> propertyIds, DateOnly today, CancellationToken cancellationToken)
    {
        if (propertyIds.Count == 0)
        {
            return new Dictionary<Guid, PropertyStats>();
        }

        // Beds without an active tenancy: occupancy follows bed and room status.
        var freeBeds = await (
                from bed in db.Beds
                join room in db.Rooms on bed.RoomId equals room.Id
                where propertyIds.Contains(room.PropertyId)
                      && room.Status != RoomStatus.Archived
                      && bed.Status != BedStatus.Archived
                      && !db.RentAgreements.Any(a => a.BedId == bed.Id && a.Status == AgreementStatus.Active)
                group bed by new { room.PropertyId, RoomStatus = room.Status, BedStatus = bed.Status }
                into g
                select new { g.Key.PropertyId, g.Key.RoomStatus, g.Key.BedStatus, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Beds with an active tenancy: occupied, or reserved while the move-in date is still ahead.
        var tenanted = await (
                from agreement in db.RentAgreements
                join bed in db.Beds on agreement.BedId equals bed.Id
                join room in db.Rooms on bed.RoomId equals room.Id
                where propertyIds.Contains(room.PropertyId)
                      && agreement.Status == AgreementStatus.Active
                      && room.Status != RoomStatus.Archived
                      && bed.Status != BedStatus.Archived
                group agreement by new { room.PropertyId, Upcoming = agreement.StartDate > today }
                into g
                select new { g.Key.PropertyId, g.Key.Upcoming, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var roomCounts = await db.Rooms
            .Where(r => propertyIds.Contains(r.PropertyId) && r.Status != RoomStatus.Archived)
            .GroupBy(r => r.PropertyId)
            .Select(g => new { PropertyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PropertyId, x => x.Count, cancellationToken);

        return propertyIds.ToDictionary(
            id => id,
            id =>
            {
                var beds = freeBeds
                    .Where(g => g.PropertyId == id)
                    .SelectMany(g => Enumerable.Repeat(Occupancy.Derive(g.BedStatus, g.RoomStatus, TenancyState.None), g.Count))
                    .Concat(tenanted
                        .Where(g => g.PropertyId == id)
                        .SelectMany(g => Enumerable.Repeat(g.Upcoming ? BedOccupancy.Reserved : BedOccupancy.Occupied, g.Count)));
                return new PropertyStats(roomCounts.GetValueOrDefault(id), OccupancySummary.From(beds));
            });
    }

    public static async Task<IReadOnlyList<RoomDto>> RoomsAsync(
        IAppDbContext db, IQueryable<Room> rooms, DateOnly today, bool includeTenantNames, CancellationToken cancellationToken)
    {
        // Natural-ish order: "2" before "10", "101" before "201".
        var roomList = await rooms.AsNoTracking()
            .OrderBy(r => r.RoomNumber.Length).ThenBy(r => r.RoomNumber)
            .ToListAsync(cancellationToken);
        if (roomList.Count == 0)
        {
            return [];
        }

        var roomIds = roomList.Select(r => r.Id).ToList();
        var beds = await db.Beds.AsNoTracking()
            .Where(b => roomIds.Contains(b.RoomId) && b.Status != BedStatus.Archived)
            .OrderBy(b => b.Label.Length).ThenBy(b => b.Label)
            .ToListAsync(cancellationToken);
        var tenancies = await TenanciesForBedsAsync(db, [.. beds.Select(b => b.Id)], today, includeTenantNames, cancellationToken);
        var bedsByRoom = beds.ToLookup(b => b.RoomId);

        return [.. roomList.Select(room => ToDto(room, bedsByRoom[room.Id], tenancies))];
    }

    public static async Task<IReadOnlyDictionary<Guid, BedTenancy>> TenanciesForBedsAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> bedIds, DateOnly today, bool includeTenantNames, CancellationToken cancellationToken)
    {
        if (bedIds.Count == 0)
        {
            return new Dictionary<Guid, BedTenancy>();
        }

        var rows = await (
                from agreement in db.RentAgreements
                join tenant in db.Tenants on agreement.TenantId equals tenant.Id
                where bedIds.Contains(agreement.BedId) && agreement.Status == AgreementStatus.Active
                select new { agreement.BedId, agreement.StartDate, tenant.Id, tenant.FullName })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.BedId,
            r => new BedTenancy(
                r.StartDate > today ? TenancyState.Upcoming : TenancyState.Current,
                includeTenantNames ? new BedTenant(r.Id, r.FullName, r.StartDate) : null));
    }

    public static RoomDto ToDto(Room room, IEnumerable<Bed> activeBeds, IReadOnlyDictionary<Guid, BedTenancy> tenancies)
    {
        var bedDtos = activeBeds
            .Select(b =>
            {
                var tenancy = tenancies.GetValueOrDefault(b.Id);
                return BedDto.From(b, room.Status, tenancy?.State ?? TenancyState.None, tenancy?.Tenant);
            })
            .ToList();
        var occupancy = room.IsArchived ? OccupancySummary.Empty : OccupancySummary.From(bedDtos.Select(b => b.Occupancy));
        return new RoomDto(room.Id, room.PropertyId, room.RoomNumber, room.RoomType, room.Capacity, room.Status, bedDtos, occupancy);
    }
}

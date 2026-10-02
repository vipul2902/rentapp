using Microsoft.EntityFrameworkCore;
using RentApp.Application.Common.Abstractions;
using RentApp.Domain.Properties;

namespace RentApp.Application.Properties;

/// <summary>
/// Occupancy is computed from bed and room status (and, from Phase 4, active tenancies) at read time,
/// never stored, so it cannot drift. Aggregation happens in SQL; only grouped counts come back.
/// </summary>
internal static class OccupancyQueries
{
    internal sealed record PropertyStats(int RoomCount, OccupancySummary Occupancy);

    public static async Task<IReadOnlyDictionary<Guid, PropertyStats>> ForPropertiesAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> propertyIds, CancellationToken cancellationToken)
    {
        if (propertyIds.Count == 0)
        {
            return new Dictionary<Guid, PropertyStats>();
        }

        var bedGroups = await (
                from bed in db.Beds
                join room in db.Rooms on bed.RoomId equals room.Id
                where propertyIds.Contains(room.PropertyId)
                      && room.Status != RoomStatus.Archived
                      && bed.Status != BedStatus.Archived
                group bed by new { room.PropertyId, RoomStatus = room.Status, BedStatus = bed.Status }
                into g
                select new { g.Key.PropertyId, g.Key.RoomStatus, g.Key.BedStatus, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var roomCounts = await db.Rooms
            .Where(r => propertyIds.Contains(r.PropertyId) && r.Status != RoomStatus.Archived)
            .GroupBy(r => r.PropertyId)
            .Select(g => new { PropertyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PropertyId, x => x.Count, cancellationToken);

        return propertyIds.ToDictionary(
            id => id,
            id => new PropertyStats(
                roomCounts.GetValueOrDefault(id),
                OccupancySummary.From(bedGroups
                    .Where(g => g.PropertyId == id)
                    // Phase 4: hasActiveTenancy comes from active rent agreements.
                    .SelectMany(g => Enumerable.Repeat(Occupancy.Derive(g.BedStatus, g.RoomStatus, hasActiveTenancy: false), g.Count)))));
    }

    public static async Task<IReadOnlyList<RoomDto>> RoomsAsync(
        IAppDbContext db, IQueryable<Room> rooms, CancellationToken cancellationToken)
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
        var bedsByRoom = beds.ToLookup(b => b.RoomId);

        return [.. roomList.Select(room => ToDto(room, bedsByRoom[room.Id]))];
    }

    public static RoomDto ToDto(Room room, IEnumerable<Bed> activeBeds)
    {
        // Phase 4: hasActiveTenancy comes from active rent agreements.
        var bedDtos = activeBeds.Select(b => BedDto.From(b, room.Status, hasActiveTenancy: false)).ToList();
        var occupancy = room.IsArchived ? OccupancySummary.Empty : OccupancySummary.From(bedDtos.Select(b => b.Occupancy));
        return new RoomDto(room.Id, room.PropertyId, room.RoomNumber, room.RoomType, room.Capacity, room.Status, bedDtos, occupancy);
    }
}

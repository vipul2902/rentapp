using Microsoft.EntityFrameworkCore;
using RentApp.Application.Audit;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Security;
using RentApp.Application.Common.Time;
using RentApp.Domain.Properties;
using RentApp.Domain.Tenants;
using RentApp.Domain.Users;

namespace RentApp.Application.Properties;

/// <summary>
/// Rooms and their beds. Viewing needs ViewProperties; changes are owner-only. Anything that depends on a
/// room's bed count (adding beds, changing capacity) locks the room row first, so concurrent requests
/// cannot push a room past its capacity. Rooms and beds with tenants cannot be archived or taken out of use.
/// </summary>
public sealed class RoomService(
    IAppDbContext db, AuditWriter audit, ICurrentUser currentUser, TimeProvider clock, OrganizationClock calendar)
{
    /// <summary>Tenant names on beds are only shown to callers who may view tenants.</summary>
    private bool IncludeTenantNames => currentUser.HasPermission(StaffPermissions.ViewTenants);

    public async Task<IReadOnlyList<RoomDto>> ListForPropertyAsync(Guid propertyId, bool includeArchived, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewProperties);
        if (!await db.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
        {
            throw PropertyErrors.PropertyNotFound();
        }

        var rooms = db.Rooms.Where(r => r.PropertyId == propertyId && (includeArchived || r.Status != RoomStatus.Archived));
        return await OccupancyQueries.RoomsAsync(db, rooms, await calendar.TodayAsync(cancellationToken), IncludeTenantNames, cancellationToken);
    }

    public async Task<RoomDto> GetAsync(Guid roomId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewProperties);
        var rooms = await OccupancyQueries.RoomsAsync(
            db, db.Rooms.Where(r => r.Id == roomId), await calendar.TodayAsync(cancellationToken), IncludeTenantNames, cancellationToken);
        return rooms.SingleOrDefault() ?? throw PropertyErrors.RoomNotFound();
    }

    public async Task<RoomDto> CreateAsync(Guid propertyId, CreateRoomRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
                       ?? throw PropertyErrors.PropertyNotFound();
        if (property.IsArchived)
        {
            throw PropertyErrors.PropertyArchived();
        }

        var roomNumber = request.RoomNumber.Trim();
        await EnsureRoomNumberFreeAsync(propertyId, roomNumber, exceptRoomId: null, cancellationToken);

        var room = Room.Create(currentUser.OrganizationId, propertyId, roomNumber, request.RoomType, request.Capacity);
        db.Rooms.Add(room);

        var beds = request.CreateBeds
            ? Enumerable.Range(0, room.Capacity)
                .Select(i => Bed.Create(currentUser.OrganizationId, room.Id, Occupancy.DefaultBedLabel(i), request.DefaultMonthlyRent))
                .ToList()
            : [];
        db.Beds.AddRange(beds);

        audit.Record(PropertyAuditActions.RoomCreated, nameof(Room), room.Id, new { room.RoomNumber, room.Capacity, beds = beds.Count });
        await SaveAsync(onUniqueViolation: () => PropertyErrors.RoomNumberTaken(roomNumber), cancellationToken);
        return OccupancyQueries.ToDto(room, beds, new Dictionary<Guid, OccupancyQueries.BedTenancy>());
    }

    public async Task<RoomDto> UpdateAsync(Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var room = await LockRoomAsync(roomId, cancellationToken);
        if (room.IsArchived)
        {
            throw PropertyErrors.RoomArchived();
        }

        var roomNumber = request.RoomNumber.Trim();
        if (roomNumber != room.RoomNumber)
        {
            await EnsureRoomNumberFreeAsync(room.PropertyId, roomNumber, room.Id, cancellationToken);
        }

        var activeBeds = await ActiveBedsAsync(room.Id, cancellationToken);
        if (request.Capacity < activeBeds.Count)
        {
            throw PropertyErrors.CapacityBelowBeds(activeBeds.Count);
        }

        if (request.Status == RoomStatus.Unavailable && room.Status != RoomStatus.Unavailable && await RoomHasTenantsAsync(room.Id, cancellationToken))
        {
            throw PropertyErrors.RoomHasTenants();
        }

        room.Update(roomNumber, request.RoomType, request.Capacity, request.Status, activeBeds.Count);
        await SaveAsync(onUniqueViolation: () => PropertyErrors.RoomNumberTaken(roomNumber), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var tenancies = await OccupancyQueries.TenanciesForBedsAsync(
            db, [.. activeBeds.Select(b => b.Id)], await calendar.TodayAsync(cancellationToken), IncludeTenantNames, cancellationToken);
        return OccupancyQueries.ToDto(room, activeBeds, tenancies);
    }

    /// <summary>Archives the room and all of its beds. The room number becomes free for reuse.</summary>
    public async Task ArchiveAsync(Guid roomId, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var room = await LockRoomAsync(roomId, cancellationToken);
        if (room.IsArchived)
        {
            return;
        }

        if (await RoomHasTenantsAsync(room.Id, cancellationToken))
        {
            throw PropertyErrors.RoomHasTenants();
        }

        var now = clock.GetUtcNow();
        await db.Beds
            .Where(b => b.RoomId == room.Id && b.Status != BedStatus.Archived)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BedStatus.Archived).SetProperty(b => b.UpdatedAt, now), cancellationToken);

        room.Archive();
        audit.Record(PropertyAuditActions.RoomArchived, nameof(Room), room.Id, new { room.RoomNumber });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BedDto>> ListBedsAsync(Guid roomId, CancellationToken cancellationToken) =>
        (await GetAsync(roomId, cancellationToken)).Beds;

    public async Task<BedDto> GetBedAsync(Guid bedId, CancellationToken cancellationToken)
    {
        currentUser.EnsurePermission(StaffPermissions.ViewProperties);
        var bed = await FindBedAsync(bedId, cancellationToken);
        return await ToBedDtoAsync(bed, cancellationToken);
    }

    public async Task<BedDto> AddBedAsync(Guid roomId, CreateBedRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var room = await LockRoomAsync(roomId, cancellationToken);
        if (room.IsArchived)
        {
            throw PropertyErrors.RoomArchived();
        }

        if (await db.Properties.AnyAsync(p => p.Id == room.PropertyId && p.Status == PropertyStatus.Archived, cancellationToken))
        {
            throw PropertyErrors.PropertyArchived();
        }

        var activeBeds = await ActiveBedsAsync(room.Id, cancellationToken);
        if (!room.CanAddBed(activeBeds.Count))
        {
            throw PropertyErrors.RoomFull(room.RoomNumber, room.Capacity);
        }

        var usedLabels = activeBeds.Select(b => b.Label).ToList();
        var label = string.IsNullOrWhiteSpace(request.Label) ? Occupancy.NextBedLabel(usedLabels) : Bed.NormalizeLabel(request.Label);
        if (usedLabels.Contains(label, StringComparer.Ordinal))
        {
            throw PropertyErrors.BedLabelTaken(label);
        }

        var bed = Bed.Create(currentUser.OrganizationId, room.Id, label, request.DefaultMonthlyRent);
        db.Beds.Add(bed);
        audit.Record(PropertyAuditActions.BedCreated, nameof(Bed), bed.Id, new { room.RoomNumber, bed.Label });
        await SaveAsync(onUniqueViolation: () => PropertyErrors.BedLabelTaken(label), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BedDto.From(bed, room.Status, TenancyState.None, tenant: null);
    }

    public async Task<BedDto> UpdateBedAsync(Guid bedId, UpdateBedRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var bed = await FindBedAsync(bedId, cancellationToken);
        if (bed.IsArchived)
        {
            throw PropertyErrors.BedArchived();
        }

        var label = Bed.NormalizeLabel(request.Label);
        if (label != bed.Label
            && await db.Beds.AnyAsync(b => b.RoomId == bed.RoomId && b.Id != bed.Id && b.Label == label && b.Status != BedStatus.Archived, cancellationToken))
        {
            throw PropertyErrors.BedLabelTaken(label);
        }

        // A bed someone lives in (or has booked) must stay Available; Reserved/Unavailable would contradict it.
        if (request.Status != BedStatus.Available && request.Status != bed.Status && await BedHasTenantAsync(bed.Id, cancellationToken))
        {
            throw PropertyErrors.BedHasTenant(bed.Label);
        }

        bed.Update(label, request.Status, request.DefaultMonthlyRent);
        await SaveAsync(onUniqueViolation: () => PropertyErrors.BedLabelTaken(label), cancellationToken);
        return await ToBedDtoAsync(bed, cancellationToken);
    }

    public async Task ArchiveBedAsync(Guid bedId, CancellationToken cancellationToken)
    {
        currentUser.EnsureOwner();
        var bed = await FindBedAsync(bedId, cancellationToken);
        if (bed.IsArchived)
        {
            return;
        }

        if (await BedHasTenantAsync(bed.Id, cancellationToken))
        {
            throw PropertyErrors.BedHasTenant(bed.Label);
        }

        bed.Archive();
        audit.Record(PropertyAuditActions.BedArchived, nameof(Bed), bed.Id, new { bed.Label });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<BedDto> ToBedDtoAsync(Bed bed, CancellationToken cancellationToken)
    {
        var roomStatus = await db.Rooms.Where(r => r.Id == bed.RoomId).Select(r => r.Status).SingleAsync(cancellationToken);
        var tenancies = await OccupancyQueries.TenanciesForBedsAsync(
            db, [bed.Id], await calendar.TodayAsync(cancellationToken), IncludeTenantNames, cancellationToken);
        var tenancy = tenancies.GetValueOrDefault(bed.Id);
        return BedDto.From(bed, roomStatus, tenancy?.State ?? TenancyState.None, tenancy?.Tenant);
    }

    /// <summary>
    /// Takes a row lock on the room (released at commit) by touching it, then loads it. Organization
    /// filters apply to both statements, so another organization's room is simply not found.
    /// </summary>
    private async Task<Room> LockRoomAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var locked = await db.Rooms.Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedAt, now), cancellationToken);
        if (locked == 0)
        {
            throw PropertyErrors.RoomNotFound();
        }

        return await db.Rooms.SingleAsync(r => r.Id == roomId, cancellationToken);
    }

    private Task<bool> RoomHasTenantsAsync(Guid roomId, CancellationToken cancellationToken) =>
        db.RentAgreements.AnyAsync(a => a.RoomId == roomId && a.Status == AgreementStatus.Active, cancellationToken);

    private Task<bool> BedHasTenantAsync(Guid bedId, CancellationToken cancellationToken) =>
        db.RentAgreements.AnyAsync(a => a.BedId == bedId && a.Status == AgreementStatus.Active, cancellationToken);

    private Task<List<Bed>> ActiveBedsAsync(Guid roomId, CancellationToken cancellationToken) =>
        db.Beds.Where(b => b.RoomId == roomId && b.Status != BedStatus.Archived)
            .OrderBy(b => b.Label.Length).ThenBy(b => b.Label)
            .ToListAsync(cancellationToken);

    private async Task<Bed> FindBedAsync(Guid bedId, CancellationToken cancellationToken) =>
        await db.Beds.SingleOrDefaultAsync(b => b.Id == bedId, cancellationToken) ?? throw PropertyErrors.BedNotFound();

    private async Task EnsureRoomNumberFreeAsync(Guid propertyId, string roomNumber, Guid? exceptRoomId, CancellationToken cancellationToken)
    {
        if (await db.Rooms.AnyAsync(
                r => r.PropertyId == propertyId && r.RoomNumber == roomNumber && r.Status != RoomStatus.Archived && r.Id != exceptRoomId,
                cancellationToken))
        {
            throw PropertyErrors.RoomNumberTaken(roomNumber);
        }
    }

    private async Task SaveAsync(Func<AppException> onUniqueViolation, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            throw onUniqueViolation();
        }
    }
}

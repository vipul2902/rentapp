using Microsoft.EntityFrameworkCore;
using RentApp.Application.Common.Abstractions;
using RentApp.Domain.Tenants;

namespace RentApp.Application.Tenants;

internal static class TenancyQueries
{
    /// <summary>Tenancies with property/room/bed names, newest first, keyed by tenant.</summary>
    public static async Task<ILookup<Guid, TenancyDto>> ForTenantsAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> tenantIds, bool activeOnly, DateOnly today, CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return Array.Empty<TenancyDto>().ToLookup(_ => Guid.Empty);
        }

        var rows = await (
                from a in db.RentAgreements
                join p in db.Properties on a.PropertyId equals p.Id
                join r in db.Rooms on a.RoomId equals r.Id
                join b in db.Beds on a.BedId equals b.Id
                where tenantIds.Contains(a.TenantId) && (!activeOnly || a.Status == AgreementStatus.Active)
                orderby a.Status, a.StartDate descending, a.CreatedAt descending
                select new { Agreement = a, PropertyName = p.Name, r.RoomNumber, BedLabel = b.Label })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.ToLookup(
            x => x.Agreement.TenantId,
            x => ToDto(x.Agreement, x.PropertyName, x.RoomNumber, x.BedLabel, today));
    }

    public static TenancyDto ToDto(RentAgreement a, string propertyName, string roomNumber, string bedLabel, DateOnly today) => new(
        a.Id, a.PropertyId, propertyName, a.RoomId, roomNumber, a.BedId, bedLabel,
        a.MonthlyRent, a.SecurityDeposit, a.RentDueDay, a.StartDate, a.EndDate, a.Status, a.StateOn(today), a.EndReason);
}

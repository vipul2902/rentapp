using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Caching;
using RentApp.Application.Common.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Common.Security;
using RentApp.Application.Common.Time;
using RentApp.Application.Payments;
using RentApp.Application.Properties;
using RentApp.Application.Reminders;
using RentApp.Application.Rent;
using RentApp.Domain.Payments;
using RentApp.Domain.Properties;
using RentApp.Domain.Rent;
using RentApp.Domain.Users;

namespace RentApp.Application.Dashboard;

/// <summary>Occupancy across active properties. Present for users who can view properties.</summary>
public sealed record DashboardOccupancy(int Properties, int Rooms, OccupancySummary Beds);

/// <summary>
/// This month's rent: what was billed, what was waived, what is expected (billed − waived), what has been
/// collected against it, and the share collected as a whole percentage (rounded down).
/// </summary>
public sealed record MonthProgress(DateOnly Month, decimal Billed, decimal Waived, decimal Expected, decimal Collected, decimal Outstanding, int PercentCollected)
{
    public static int Percent(decimal collected, decimal expected) =>
        expected <= 0 ? 0 : (int)Math.Min(100, decimal.Floor(collected * 100 / expected));
}

/// <summary>Money figures. Present for users who can view tenants.</summary>
public sealed record DashboardRent(
    AmountCount CollectedThisMonth,
    MonthProgress ThisMonth,
    AmountCount Outstanding,
    AmountCount Overdue,
    AmountCount DueToday,
    AmountCount DueThisWeek,
    IReadOnlyList<RentChargeDto> OverdueList,
    IReadOnlyList<PaymentSummary> RecentPayments);

/// <summary>
/// Everything the home screen shows, in one request. Sections the user may not see are null.
/// RemindersToSend (needs SendReminders) counts dues the reminder queue suggests today.
/// </summary>
public sealed record DashboardDto(DateOnly Today, DateTimeOffset GeneratedAt, DashboardOccupancy? Occupancy, DashboardRent? Rent, int? RemindersToSend);

/// <summary>
/// The owner's daily numbers (spec §14). Results are cached in Redis for a short time, keyed by the
/// organization's data version, so any committed change shows up on the next request. If Redis is down,
/// the numbers are computed from PostgreSQL every time.
/// </summary>
public sealed class DashboardService(
    IAppDbContext db,
    ICurrentUser currentUser,
    OrganizationClock calendar,
    RentService rent,
    PaymentService payments,
    ReminderService reminders,
    IAppCache cache,
    TimeProvider clock)
{
    public static readonly TimeSpan CacheTimeToLive = TimeSpan.FromSeconds(60);
    private const int ListSize = 5;

    public async Task<DashboardDto> GetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var seesBeds = currentUser.HasPermission(StaffPermissions.ViewProperties);
        var seesRent = currentUser.HasPermission(StaffPermissions.ViewTenants);
        var reminds = currentUser.HasPermission(StaffPermissions.SendReminders);
        if (propertyId is { } id && !await db.Properties.AnyAsync(p => p.Id == id, cancellationToken))
        {
            throw new NotFoundException("PROPERTY_NOT_FOUND", "The requested property was not found.");
        }

        var today = await calendar.TodayAsync(cancellationToken);
        var version = await cache.OrganizationVersionAsync(currentUser.OrganizationId, cancellationToken);
        // What a user may see is part of the key, so a staff member never gets the owner's cached figures.
        var key = version is { } v
            ? string.Create(CultureInfo.InvariantCulture,
                $"dashboard:{currentUser.OrganizationId:N}:v{v}:{today:yyyy-MM-dd}:{(seesBeds ? 'b' : '-')}{(seesRent ? 'r' : '-')}{(reminds ? 'm' : '-')}:{propertyId?.ToString("N") ?? "all"}")
            : null;

        if (key is not null && await cache.GetAsync<DashboardDto>(key, cancellationToken) is { } cached)
        {
            return cached;
        }

        var dashboard = new DashboardDto(
            today,
            clock.GetUtcNow(),
            seesBeds ? await OccupancyAsync(propertyId, today, cancellationToken) : null,
            seesRent ? await RentAsync(propertyId, today, cancellationToken) : null,
            reminds ? (await reminders.QueueAsync(null, cancellationToken)).Items.Count(s => propertyId == null || s.Charge.PropertyId == propertyId) : null);

        if (key is not null)
        {
            await cache.SetAsync(key, dashboard, CacheTimeToLive, cancellationToken);
        }

        return dashboard;
    }

    private async Task<DashboardOccupancy> OccupancyAsync(Guid? propertyId, DateOnly today, CancellationToken cancellationToken)
    {
        var propertyIds = await db.Properties
            .Where(p => p.Status == PropertyStatus.Active && (propertyId == null || p.Id == propertyId))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var stats = (await OccupancyQueries.ForPropertiesAsync(db, propertyIds, today, cancellationToken)).Values.ToList();
        var beds = stats.Select(s => s.Occupancy).Aggregate(OccupancySummary.Empty, (sum, o) => new OccupancySummary(
            sum.TotalBeds + o.TotalBeds, sum.Occupied + o.Occupied, sum.Vacant + o.Vacant, sum.Reserved + o.Reserved, sum.Unavailable + o.Unavailable));
        return new DashboardOccupancy(propertyIds.Count, stats.Sum(s => s.RoomCount), beds);
    }

    private async Task<DashboardRent> RentAsync(Guid? propertyId, DateOnly today, CancellationToken cancellationToken)
    {
        var summary = await rent.SummaryAsync(propertyId, cancellationToken);
        var monthStart = RentSchedule.MonthStart(today);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // Money received this month (by payment date), counted through allocations so a property filter
        // only counts what was paid towards that property's dues.
        var received = await (
                from a in db.PaymentAllocations
                join p in db.Payments on a.PaymentId equals p.Id
                join c in db.RentCharges on a.RentChargeId equals c.Id
                where p.Status == PaymentStatus.Recorded && p.PaymentDate >= monthStart && p.PaymentDate <= monthEnd
                      && (propertyId == null || c.PropertyId == propertyId)
                select new { a.PaymentId, a.AllocatedAmount })
            .ToListAsync(cancellationToken);
        var collected = new AmountCount(received.Sum(r => r.AllocatedAmount), received.Select(r => r.PaymentId).Distinct().Count());

        var month = await db.RentCharges
            .Where(c => c.CancelledAt == null && c.PeriodStart == monthStart && (propertyId == null || c.PropertyId == propertyId))
            .GroupBy(_ => 1)
            .Select(g => new { Billed = g.Sum(c => c.Amount), Paid = g.Sum(c => c.PaidAmount), Waived = g.Sum(c => c.AdjustedAmount), Balance = g.Sum(c => c.BalanceAmount) })
            .SingleOrDefaultAsync(cancellationToken);
        var billed = month?.Billed ?? 0m;
        var waived = month?.Waived ?? 0m;
        var paid = month?.Paid ?? 0m;
        var expected = billed - waived;
        var thisMonth = new MonthProgress(monthStart, billed, waived, expected, paid, month?.Balance ?? 0m, MonthProgress.Percent(paid, expected));

        var overdue = await rent.OverdueAsync(new PageQuery { Page = 1, PageSize = ListSize }, propertyId, cancellationToken);
        var recent = await payments.ListAsync(
            new PaymentListQuery { Page = 1, PageSize = ListSize, PropertyId = propertyId, IncludeVoided = false }, cancellationToken);

        return new DashboardRent(collected, thisMonth, summary.Outstanding, summary.Overdue, summary.DueToday, summary.DueThisWeek, overdue.Items, recent.Items);
    }
}

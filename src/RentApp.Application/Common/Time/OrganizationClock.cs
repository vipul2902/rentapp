using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Security;
using RentApp.Domain.Common;
using RentApp.Domain.Organizations;

namespace RentApp.Application.Common.Time;

/// <summary>"Today" in the caller's organization time zone (scoped: the zone is read once per request).</summary>
public sealed class OrganizationClock(
    IAppDbContext db, ICurrentUser currentUser, [FromKeyedServices(BusinessTime.Key)] TimeProvider clock)
{
    private string? _timeZone;

    public async Task<DateOnly> TodayAsync(CancellationToken cancellationToken)
    {
        _timeZone ??= await db.Organizations
                          .Where(o => o.Id == currentUser.OrganizationId)
                          .Select(o => o.TimeZone)
                          .SingleOrDefaultAsync(cancellationToken)
                      ?? Organization.DefaultTimeZone;

        return OrganizationCalendar.Today(clock.GetUtcNow(), _timeZone);
    }
}

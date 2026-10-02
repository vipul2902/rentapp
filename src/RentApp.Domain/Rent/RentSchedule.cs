using RentApp.Domain.Tenants;

namespace RentApp.Domain.Rent;

/// <summary>The scheduling facts the rent engine needs about one tenancy.</summary>
public sealed record TenancyTerms(
    Guid AgreementId,
    Guid TenantId,
    decimal MonthlyRent,
    int RentDueDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCancelled);

/// <summary>A monthly charge the engine has decided should exist.</summary>
public sealed record ScheduledCharge(Guid AgreementId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate, decimal Amount);

/// <summary>
/// Pure rules for monthly rent (spec §9). No I/O, so every edge case is unit-tested:
/// <list type="bullet">
/// <item>One charge per tenant per calendar month, for the full monthly rent (no proration in V1;
/// owners can waive part of a charge).</item>
/// <item>The month is charged by the tenant's earliest tenancy that overlaps it, so moving beds
/// mid-month never charges twice.</item>
/// <item>Due date = the due day, clamped to the month's last day; in the move-in month it is never
/// before the move-in date.</item>
/// <item>A charge is created once its due date is within <c>lookaheadDays</c> of today, so upcoming
/// dues are visible a week ahead.</item>
/// <item>Cancelled bookings are never charged.</item>
/// </list>
/// </summary>
public static class RentSchedule
{
    public const int DefaultLookaheadDays = 7;

    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly MonthEnd(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    /// <summary>The due day in a month, moved back to the last day for short months (31 → 30, 30 → 28/29).</summary>
    public static DateOnly DueDateIn(DateOnly monthStart, int dueDay) =>
        new(monthStart.Year, monthStart.Month, Math.Min(dueDay, DateTime.DaysInMonth(monthStart.Year, monthStart.Month)));

    /// <summary>All charges that should exist today for one tenant's tenancies.</summary>
    public static IReadOnlyList<ScheduledCharge> ChargesFor(
        IReadOnlyCollection<TenancyTerms> tenantTenancies, DateOnly today, int lookaheadDays = DefaultLookaheadDays)
    {
        var horizon = today.AddDays(lookaheadDays);
        var tenancies = tenantTenancies.Where(t => !t.IsCancelled).OrderBy(t => t.StartDate).ToList();
        var charges = new List<ScheduledCharge>();

        foreach (var tenancy in tenancies)
        {
            var lastMonth = MonthStart(tenancy.EndDate ?? horizon);
            for (var month = MonthStart(tenancy.StartDate); month <= lastMonth; month = month.AddMonths(1))
            {
                if (!Overlaps(tenancy, month))
                {
                    continue;
                }

                // Another, earlier tenancy of the same tenant already covers part of this month: it pays for it.
                var chargedElsewhere = tenancies.Any(other =>
                    other.AgreementId != tenancy.AgreementId && other.StartDate < tenancy.StartDate && Overlaps(other, month));
                if (chargedElsewhere)
                {
                    continue;
                }

                var due = DueDateIn(month, tenancy.RentDueDay);
                if (due < tenancy.StartDate)
                {
                    due = tenancy.StartDate;
                }

                if (due > horizon)
                {
                    break;
                }

                charges.Add(new ScheduledCharge(tenancy.AgreementId, month, MonthEnd(month), due, tenancy.MonthlyRent));
            }
        }

        return charges;
    }

    private static bool Overlaps(TenancyTerms tenancy, DateOnly monthStart) =>
        tenancy.StartDate <= MonthEnd(monthStart) && (tenancy.EndDate is null || tenancy.EndDate >= monthStart);
}

/// <summary>What the owner sees for a charge. Derived from dates and amounts at read time, never stored.</summary>
public enum RentStatus
{
    Upcoming = 1,
    DueToday = 2,
    Overdue = 3,
    PartiallyPaid = 4,
    Paid = 5,

    /// <summary>Settled entirely by waivers/adjustments, with no payment.</summary>
    Waived = 6,

    /// <summary>No longer owed (the tenant moved out or the booking was cancelled before it was paid).</summary>
    Cancelled = 7,
}

public static class RentStatusRules
{
    public static RentStatus Derive(DateOnly dueDate, decimal paid, decimal balance, bool cancelled, DateOnly today)
    {
        if (cancelled)
        {
            return RentStatus.Cancelled;
        }

        if (balance <= 0)
        {
            return paid > 0 ? RentStatus.Paid : RentStatus.Waived;
        }

        if (dueDate < today)
        {
            return RentStatus.Overdue;
        }

        if (dueDate == today)
        {
            return RentStatus.DueToday;
        }

        return paid > 0 ? RentStatus.PartiallyPaid : RentStatus.Upcoming;
    }

    public static int DaysOverdue(DateOnly dueDate, decimal balance, bool cancelled, DateOnly today) =>
        !cancelled && balance > 0 && dueDate < today ? today.DayNumber - dueDate.DayNumber : 0;

    /// <summary>Terms of an agreement as the scheduler needs them.</summary>
    public static TenancyTerms TermsOf(RentAgreement a) => new(
        a.Id, a.TenantId, a.MonthlyRent, a.RentDueDay, a.StartDate, a.EndDate, a.EndReason == AgreementEndReason.Cancelled);
}

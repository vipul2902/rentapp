using RentApp.Domain.Rent;

namespace RentApp.UnitTests.Domain;

public class RentScheduleTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static TenancyTerms Tenancy(string start, string? end = null, int dueDay = 5, decimal rent = 8500m, bool cancelled = false) =>
        new(Guid.NewGuid(), Tenant, rent, dueDay, D(start), end is null ? null : D(end), cancelled);

    private static List<string> Months(IEnumerable<ScheduledCharge> charges) => [.. charges.Select(c => c.PeriodStart.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture))];

    // ---- Due dates -------------------------------------------------------------------------------

    [Theory]
    [InlineData("2026-10-01", 5, "2026-10-05")]
    [InlineData("2026-09-01", 31, "2026-09-30")]
    [InlineData("2026-02-01", 31, "2026-02-28")]
    [InlineData("2026-02-01", 29, "2026-02-28")]
    [InlineData("2028-02-01", 31, "2028-02-29")] // leap year
    [InlineData("2028-02-01", 29, "2028-02-29")]
    [InlineData("2026-12-01", 31, "2026-12-31")]
    public void DueDayIsClampedToTheEndOfShortMonths(string month, int dueDay, string expected)
    {
        Assert.Equal(D(expected), RentSchedule.DueDateIn(D(month), dueDay));
    }

    [Fact]
    public void MoveInMonthIsNeverDueBeforeTheMoveIn()
    {
        var charges = RentSchedule.ChargesFor([Tenancy("2026-10-20", dueDay: 5)], today: D("2026-10-25"));

        var october = Assert.Single(charges);
        Assert.Equal(D("2026-10-20"), october.DueDate);
    }

    // ---- Which months are charged ----------------------------------------------------------------

    [Fact]
    public void BackdatedTenancyGetsEveryMonthUpToToday()
    {
        var charges = RentSchedule.ChargesFor([Tenancy("2026-08-20")], today: D("2026-10-12"));

        Assert.Equal(["2026-08", "2026-09", "2026-10"], Months(charges));
        Assert.All(charges, c => Assert.Equal(8500m, c.Amount));
        Assert.Equal((D("2026-08-01"), D("2026-08-31")), (charges[0].PeriodStart, charges[0].PeriodEnd));
    }

    [Theory]
    [InlineData("2026-10-28", 5, new[] { "2026-10" })]             // Nov 5 is 8 days away: not yet
    [InlineData("2026-10-29", 5, new[] { "2026-10", "2026-11" })]  // Nov 5 is exactly 7 days away
    [InlineData("2026-12-26", 1, new[] { "2026-10", "2026-11", "2026-12", "2027-01" })] // across the year end
    public void NextMonthAppearsAWeekBeforeItIsDue(string today, int dueDay, string[] expected)
    {
        var charges = RentSchedule.ChargesFor([Tenancy("2026-10-01", dueDay: dueDay)], D(today));

        Assert.Equal(expected, Months(charges));
    }

    [Fact]
    public void NothingIsChargedAfterMoveOut()
    {
        var charges = RentSchedule.ChargesFor([Tenancy("2026-08-01", end: "2026-10-10")], today: D("2026-12-15"));

        Assert.Equal(["2026-08", "2026-09", "2026-10"], Months(charges));
    }

    [Fact]
    public void MovingOutOnTheLastDayOfAMonthDoesNotChargeTheNextMonth()
    {
        var charges = RentSchedule.ChargesFor([Tenancy("2026-09-01", end: "2026-09-30")], today: D("2026-10-20"));

        Assert.Equal(["2026-09"], Months(charges));
    }

    [Fact]
    public void FutureBookingIsChargedOnlyWhenItsDueDateIsNear()
    {
        Assert.Empty(RentSchedule.ChargesFor([Tenancy("2026-11-20")], today: D("2026-11-01")));
        Assert.Equal(["2026-11"], Months(RentSchedule.ChargesFor([Tenancy("2026-11-20")], today: D("2026-11-13"))));
    }

    [Fact]
    public void CancelledBookingsAreNeverCharged()
    {
        Assert.Empty(RentSchedule.ChargesFor([Tenancy("2026-08-01", end: "2026-08-01", cancelled: true)], today: D("2026-10-12")));
    }

    [Fact]
    public void MovingBedsMidMonthChargesTheMonthOnce()
    {
        var oldBed = Tenancy("2026-09-01", end: "2026-10-14", rent: 8000m);
        var newBed = Tenancy("2026-10-15", rent: 9000m);

        var charges = RentSchedule.ChargesFor([newBed, oldBed], today: D("2026-11-03"));

        Assert.Equal(["2026-09", "2026-10", "2026-11"], Months(charges));
        Assert.Equal([oldBed.AgreementId, oldBed.AgreementId, newBed.AgreementId], charges.Select(c => c.AgreementId));
        Assert.Equal([8000m, 8000m, 9000m], charges.Select(c => c.Amount));
    }

    [Fact]
    public void MovingBedsOnTheFirstOfTheMonthChargesTheNewBed()
    {
        var oldBed = Tenancy("2026-09-01", end: "2026-09-30", rent: 8000m);
        var newBed = Tenancy("2026-10-01", rent: 9000m);

        var charges = RentSchedule.ChargesFor([oldBed, newBed], today: D("2026-10-10"));

        Assert.Equal([8000m, 9000m], charges.Select(c => c.Amount));
    }

    [Fact]
    public void ReturningTenantIsChargedForEachStay()
    {
        var first = Tenancy("2026-01-10", end: "2026-02-20");
        var second = Tenancy("2026-05-01");

        var charges = RentSchedule.ChargesFor([first, second], today: D("2026-06-10"));

        Assert.Equal(["2026-01", "2026-02", "2026-05", "2026-06"], Months(charges));
    }

    [Fact]
    public void LeapDayMoveInIsHandled()
    {
        var charges = RentSchedule.ChargesFor([Tenancy("2028-02-29", dueDay: 31)], today: D("2028-03-31"));

        Assert.Equal([D("2028-02-29"), D("2028-03-31")], charges.Select(c => c.DueDate));
    }

    // ---- Status ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("2026-10-05", 0, 8500, false, RentStatus.Overdue)]
    [InlineData("2026-10-12", 0, 8500, false, RentStatus.DueToday)]
    [InlineData("2026-10-20", 0, 8500, false, RentStatus.Upcoming)]
    [InlineData("2026-10-20", 5000, 3500, false, RentStatus.PartiallyPaid)]
    [InlineData("2026-10-05", 5000, 3500, false, RentStatus.Overdue)]
    [InlineData("2026-10-05", 8500, 0, false, RentStatus.Paid)]
    [InlineData("2026-10-05", 0, 0, false, RentStatus.Waived)]
    [InlineData("2026-10-05", 0, 8500, true, RentStatus.Cancelled)]
    public void StatusIsDerivedFromDatesAndAmounts(string due, int paid, int balance, bool cancelled, RentStatus expected)
    {
        Assert.Equal(expected, RentStatusRules.Derive(D(due), paid, balance, cancelled, today: D("2026-10-12")));
    }

    [Fact]
    public void MultiplePaymentsAddingUpToTheRentMeanPaid()
    {
        // ₹3,000 + ₹2,500 + ₹3,000 = ₹8,500 (spec §9).
        var paid = 3000m + 2500m + 3000m;
        Assert.Equal(RentStatus.Paid, RentStatusRules.Derive(D("2026-10-05"), paid, 8500m - paid, false, D("2026-10-12")));
    }

    [Fact]
    public void DaysOverdueCountsOnlyUnpaidPastDueCharges()
    {
        var today = D("2026-10-12");
        Assert.Equal(7, RentStatusRules.DaysOverdue(D("2026-10-05"), 8500m, false, today));
        Assert.Equal(0, RentStatusRules.DaysOverdue(D("2026-10-05"), 0m, false, today));
        Assert.Equal(0, RentStatusRules.DaysOverdue(D("2026-10-12"), 8500m, false, today));
        Assert.Equal(0, RentStatusRules.DaysOverdue(D("2026-10-05"), 8500m, true, today));
    }
}

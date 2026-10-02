using RentApp.Domain.Common;
using RentApp.Domain.Tenants;

namespace RentApp.UnitTests.Domain;

public class TenancyTests
{
    private static readonly DateOnly Today = new(2026, 10, 2);

    private static RentAgreement Agreement(DateOnly start) =>
        RentAgreement.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 8500m, 10000m, 5, start);

    [Fact]
    public void StateFollowsTheMoveInDate()
    {
        Assert.Equal(TenancyState.Current, Agreement(Today).StateOn(Today));
        Assert.Equal(TenancyState.Current, Agreement(Today.AddDays(-30)).StateOn(Today));
        Assert.Equal(TenancyState.Upcoming, Agreement(Today.AddDays(1)).StateOn(Today));

        var ended = Agreement(Today.AddDays(-30));
        ended.End(Today, AgreementEndReason.MovedOut);
        Assert.Equal(TenancyState.Ended, ended.StateOn(Today));
    }

    [Fact]
    public void EndingValidatesDatesAndCannotRepeat()
    {
        var agreement = Agreement(Today.AddDays(-10));

        Assert.Throws<ArgumentOutOfRangeException>(() => agreement.End(Today.AddDays(-11), AgreementEndReason.MovedOut));
        agreement.End(Today, AgreementEndReason.MovedOut);
        Assert.Equal((AgreementStatus.Ended, Today), (agreement.Status, agreement.EndDate!.Value));
        Assert.Throws<InvalidOperationException>(() => agreement.End(Today, AgreementEndReason.MovedOut));
    }

    [Fact]
    public void OnlyUpcomingTenanciesCanBeCancelled()
    {
        var booking = Agreement(Today.AddDays(5));
        booking.Cancel(Today);
        Assert.Equal((AgreementEndReason.Cancelled, booking.StartDate), (booking.EndReason!.Value, booking.EndDate!.Value));

        Assert.Throws<InvalidOperationException>(() => Agreement(Today).Cancel(Today));
    }

    [Theory]
    [InlineData("0", "0", 5)]
    [InlineData("8500.555", "0", 5)]
    [InlineData("8500", "-1", 5)]
    [InlineData("8500", "0", 0)]
    [InlineData("8500", "0", 32)]
    public void TermsAreValidated(string rent, string deposit, int dueDay)
    {
        var agreement = Agreement(Today);
        Assert.Throws<ArgumentOutOfRangeException>(() => agreement.UpdateTerms(
            decimal.Parse(rent, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(deposit, System.Globalization.CultureInfo.InvariantCulture),
            dueDay));
    }

    [Fact]
    public void ZeroDepositAndDueDay31AreAllowed()
    {
        var agreement = Agreement(Today);
        agreement.UpdateTerms(8500m, 0m, 31);
        Assert.Equal((0m, 31), (agreement.SecurityDeposit, agreement.RentDueDay));
    }

    [Fact]
    public void TenantPhoneIsStoredWithSearchableDigits()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "  Rahul Sharma ", " +91 98765-43210 ", " ", null, null, null);

        Assert.Equal("Rahul Sharma", tenant.FullName);
        Assert.Equal("+91 98765-43210", tenant.Phone);
        Assert.Equal("919876543210", tenant.PhoneDigits);
        Assert.Null(tenant.Email);
    }

    [Theory]
    [InlineData("2026-10-04T18:29:00Z", "2026-10-04")] // 23:59 IST
    [InlineData("2026-10-04T18:30:00Z", "2026-10-05")] // 00:00 IST next day
    public void TodayUsesTheOrganizationTimeZone(string utc, string expected)
    {
        var today = OrganizationCalendar.Today(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), "Asia/Kolkata");
        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), today);
    }

    [Fact]
    public void UnknownTimeZoneFallsBackToUtc()
    {
        var now = new DateTimeOffset(2026, 10, 4, 23, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 10, 4), OrganizationCalendar.Today(now, "Not/AZone"));
    }
}

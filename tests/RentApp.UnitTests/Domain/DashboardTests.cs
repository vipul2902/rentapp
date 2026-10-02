using RentApp.Application.Dashboard;

namespace RentApp.UnitTests.Domain;

public class DashboardTests
{
    [Theory]
    [InlineData("0", "0", 0)]
    [InlineData("2000", "17000", 11)] // 11.76% is shown as 11%: never round up to "done"
    [InlineData("16999.99", "17000", 99)]
    [InlineData("8500", "8500", 100)]
    [InlineData("100", "0", 0)] // nothing expected (all waived)
    public void PercentCollectedRoundsDownAndStaysWithinZeroToHundred(string collected, string expected, int percent)
    {
        var c = decimal.Parse(collected, System.Globalization.CultureInfo.InvariantCulture);
        var e = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(percent, MonthProgress.Percent(c, e));
    }
}

using RentApp.Infrastructure.Receipts;

namespace RentApp.UnitTests.Receipts;

public class IndianRupeesTests
{
    [Theory]
    [InlineData("0", "₹0.00")]
    [InlineData("999", "₹999.00")]
    [InlineData("8500", "₹8,500.00")]
    [InlineData("125000.5", "₹1,25,000.50")]
    [InlineData("10000000", "₹1,00,00,000.00")]
    [InlineData("1234567890.12", "₹1,23,45,67,890.12")]
    public void FormatsWithLakhAndCroreGrouping(string amount, string expected)
    {
        Assert.Equal(expected, IndianRupees.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("8500", "Rupees Eight Thousand Five Hundred Only")]
    [InlineData("125000", "Rupees One Lakh Twenty-Five Thousand Only")]
    [InlineData("10000000", "Rupees One Crore Only")]
    [InlineData("1999.50", "Rupees One Thousand Nine Hundred Ninety-Nine and Fifty Paise Only")]
    [InlineData("11", "Rupees Eleven Only")]
    public void SpellsAmountsTheIndianWay(string amount, string expected)
    {
        Assert.Equal(expected, IndianRupees.InWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }
}

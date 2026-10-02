using RentApp.Application.Common.Validation;

namespace RentApp.UnitTests.Validation;

public class MoneyAttributeTests
{
    private readonly MoneyAttribute _money = new();

    [Theory]
    [InlineData("0.01")]
    [InlineData("8500")]
    [InlineData("8500.5")]
    [InlineData("8500.55")]
    [InlineData("9999999999.99")]
    public void AcceptsValidAmounts(string amount) =>
        Assert.True(_money.IsValid(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("8500.555")]
    [InlineData("10000000000")]
    public void RejectsInvalidAmounts(string amount) =>
        Assert.False(_money.IsValid(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void NullMeansNotProvided() => Assert.True(_money.IsValid(null));
}

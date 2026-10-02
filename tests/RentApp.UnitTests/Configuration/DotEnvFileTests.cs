using RentApp.Infrastructure.Configuration;

namespace RentApp.UnitTests.Configuration;

public class DotEnvFileTests
{
    [Fact]
    public void ParseSkipsCommentsAndBlankLines()
    {
        var result = DotEnvFile.Parse(["# comment", "", "   ", "KEY=value"]).ToList();

        var pair = Assert.Single(result);
        Assert.Equal("KEY", pair.Key);
        Assert.Equal("value", pair.Value);
    }

    [Fact]
    public void ParseKeepsEqualsSignsInsideValues()
    {
        var result = DotEnvFile.Parse(["DATABASE_CONNECTION_STRING=Host=localhost;Password=a=b"]).Single();

        Assert.Equal("Host=localhost;Password=a=b", result.Value);
    }

    [Theory]
    [InlineData("KEY=\"quoted value\"", "quoted value")]
    [InlineData("KEY='single'", "single")]
    [InlineData("KEY=\"unbalanced'", "\"unbalanced'")]
    [InlineData("export KEY=exported", "exported")]
    [InlineData("KEY=", "")]
    [InlineData("  KEY  =  spaced  ", "spaced")]
    public void ParseHandlesQuotingAndWhitespace(string line, string expected)
    {
        var result = DotEnvFile.Parse([line]).Single();

        Assert.Equal("KEY", result.Key);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("NO_EQUALS_SIGN")]
    [InlineData("=value-without-key")]
    public void ParseIgnoresMalformedLines(string line)
    {
        Assert.Empty(DotEnvFile.Parse([line]));
    }
}

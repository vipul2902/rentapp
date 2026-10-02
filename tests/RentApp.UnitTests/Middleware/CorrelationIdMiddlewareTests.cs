using RentApp.Api.Middleware;

namespace RentApp.UnitTests.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("abc-123")]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("mobile_req.42")]
    public void AcceptsWellFormedIds(string value) => Assert.True(CorrelationIdMiddleware.IsValid(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<script>")]
    [InlineData("has space")]
    [InlineData("line\nbreak")]
    public void RejectsMalformedIds(string? value) => Assert.False(CorrelationIdMiddleware.IsValid(value));

    [Fact]
    public void RejectsOverlongIds() => Assert.False(CorrelationIdMiddleware.IsValid(new string('a', 65)));
}

using Microsoft.AspNetCore.Http;
using RentApp.Api.Errors;
using RentApp.Application.Common.Errors;

namespace RentApp.UnitTests.Errors;

public class GlobalExceptionHandlerTests
{
    private const string TraceId = "trace-1";

    public static TheoryData<AppException, int> AppExceptions => new()
    {
        { new ValidationException("PAYMENT_AMOUNT_INVALID", "Amount must be positive."), StatusCodes.Status400BadRequest },
        { new NotFoundException("RENT_CHARGE_NOT_FOUND", "The requested rent charge was not found."), StatusCodes.Status404NotFound },
        { new ConflictException("PAYMENT_ALREADY_VOIDED", "Already voided."), StatusCodes.Status409Conflict },
        { new ForbiddenException(ErrorCodes.Forbidden, "No access."), StatusCodes.Status403Forbidden },
        { new UnauthorizedException(ErrorCodes.Unauthorized, "Sign in."), StatusCodes.Status401Unauthorized },
    };

    [Theory]
    [MemberData(nameof(AppExceptions))]
    public void AppExceptionsKeepTheirCodeAndSafeMessage(AppException exception, int expectedStatus)
    {
        var (status, error) = GlobalExceptionHandler.Map(exception, TraceId);

        Assert.Equal(expectedStatus, status);
        Assert.Equal(exception.Code, error.Code);
        Assert.Equal(exception.Message, error.Message);
        Assert.Equal(TraceId, error.TraceId);
    }

    [Fact]
    public void ValidationExceptionCarriesFieldErrors()
    {
        var fieldErrors = new Dictionary<string, string[]> { ["amount"] = ["Amount must be positive."] };

        var (_, error) = GlobalExceptionHandler.Map(
            new ValidationException(ErrorCodes.ValidationFailed, "Invalid.", fieldErrors), TraceId);

        Assert.Same(fieldErrors, error.Errors);
    }

    [Fact]
    public void UnexpectedExceptionsBecomeGeneric500WithoutLeakingDetails()
    {
        var exception = new InvalidOperationException("Npgsql: connection to 10.0.0.5 failed, password=hunter2");

        var (status, error) = GlobalExceptionHandler.Map(exception, TraceId);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal(ErrorCodes.InternalError, error.Code);
        Assert.DoesNotContain("Npgsql", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
        Assert.Null(error.Errors);
    }
}

using RentApp.Application.Common.Security;
using RentApp.Infrastructure.Security;

namespace RentApp.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void VerifiesCorrectPasswordAndRejectsWrongOne()
    {
        var hash = _hasher.Hash("Correct-Horse-1");

        Assert.Equal(PasswordCheck.Success, _hasher.Verify(hash, "Correct-Horse-1"));
        Assert.Equal(PasswordCheck.Failed, _hasher.Verify(hash, "correct-horse-1"));
    }

    [Fact]
    public void HashesAreSaltedAndNeverContainThePassword()
    {
        var a = _hasher.Hash("Same-Password-1");
        var b = _hasher.Hash("Same-Password-1");

        Assert.NotEqual(a, b);
        Assert.DoesNotContain("Same-Password-1", a, StringComparison.Ordinal);
    }

    [Fact]
    public void GarbageHashFailsInsteadOfThrowing()
    {
        Assert.Equal(PasswordCheck.Failed, _hasher.Verify("AQAAAAEAACcQAAAA", "anything"));
    }
}

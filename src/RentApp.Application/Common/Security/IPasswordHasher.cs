namespace RentApp.Application.Common.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);
}

public enum PasswordCheck
{
    Failed = 0,
    Success = 1,

    /// <summary>Correct, but hashed with older parameters; re-hash and store.</summary>
    SuccessRehashNeeded = 2,
}

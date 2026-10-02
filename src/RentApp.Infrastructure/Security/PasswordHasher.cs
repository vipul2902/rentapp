using Microsoft.AspNetCore.Identity;
using RentApp.Application.Common.Security;
using RentApp.Domain.Users;

namespace RentApp.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity's hasher (PBKDF2-HMAC-SHA512 with per-password salt; format versioned, so
/// stronger parameters in future releases trigger a transparent re-hash at next login).
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public PasswordCheck Verify(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(null!, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
}

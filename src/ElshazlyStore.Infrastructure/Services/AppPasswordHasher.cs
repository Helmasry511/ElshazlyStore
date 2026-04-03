using ElshazlyStore.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher for strong PBKDF2 hashing.
/// </summary>
public sealed class AppPasswordHasher : IPasswordHasher
{
    // PasswordHasher<T> doesn't use T for hashing, it's just a generic parameter
    private readonly PasswordHasher<object> _inner = new();
    private static readonly object Dummy = new();

    public string Hash(string password) => _inner.HashPassword(Dummy, password);

    public bool Verify(string password, string hash)
    {
        var result = _inner.VerifyHashedPassword(Dummy, hash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

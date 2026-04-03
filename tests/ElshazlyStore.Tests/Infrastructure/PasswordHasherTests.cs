using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;

namespace ElshazlyStore.Tests.Infrastructure;

/// <summary>
/// Tests for the password hasher implementation.
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new AppPasswordHasher();

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        var hash = _hasher.Hash("TestPassword123!");
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void Hash_SamePassword_ProducesDifferentHashes()
    {
        var hash1 = _hasher.Hash("TestPassword123!");
        var hash2 = _hasher.Hash("TestPassword123!");
        Assert.NotEqual(hash1, hash2); // Salted hashing
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var password = "Correct@Password1";
        var hash = _hasher.Hash(password);
        Assert.True(_hasher.Verify(password, hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("CorrectPassword");
        Assert.False(_hasher.Verify("WrongPassword", hash));
    }

    [Fact]
    public void Verify_EmptyPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("SomePassword");
        Assert.False(_hasher.Verify("", hash));
    }
}

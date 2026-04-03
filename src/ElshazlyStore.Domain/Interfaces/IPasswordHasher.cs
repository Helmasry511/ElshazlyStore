namespace ElshazlyStore.Domain.Interfaces;

/// <summary>
/// Hashes and verifies passwords. Implementation uses ASP.NET Core Identity's hasher.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

using ElshazlyStore.Domain.Entities;

namespace ElshazlyStore.Domain.Interfaces;

/// <summary>
/// Generates JWT access tokens and opaque refresh tokens.
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(User user, IReadOnlyList<string> permissionCodes);

    /// <summary>
    /// Generates a new refresh token. Returns the raw token for the client
    /// and a <see cref="RefreshToken"/> entity with only the hash stored.
    /// </summary>
    (string RawToken, RefreshToken Entity) GenerateRefreshToken(Guid userId);

    /// <summary>
    /// Computes the HMAC-SHA256 hash used for DB storage / lookup.
    /// </summary>
    string HashToken(string rawToken);
}

namespace ElshazlyStore.Desktop.Services.Api;

/// <summary>
/// Stores JWT access/refresh tokens for the current session.
/// </summary>
public interface ITokenStore
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTime? ExpiresAtUtc { get; }
    bool IsExpired { get; }

    void SetTokens(string accessToken, string refreshToken, DateTime expiresAtUtc);
    void Clear();
}

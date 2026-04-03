namespace ElshazlyStore.Desktop.Services.Api;

/// <summary>
/// In-memory token store. Tokens are lost when the app closes.
/// Used as fallback; prefer SecureTokenStore for production.
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public bool IsExpired => ExpiresAtUtc.HasValue && DateTime.UtcNow >= ExpiresAtUtc.Value;

    public void SetTokens(string accessToken, string refreshToken, DateTime expiresAtUtc)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        ExpiresAtUtc = null;
    }
}

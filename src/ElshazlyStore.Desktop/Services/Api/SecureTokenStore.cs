using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.Services.Api;

/// <summary>
/// Persists JWT tokens to disk using DPAPI (Windows Data Protection).
/// File is encrypted per-user — only the logged-in Windows user can decrypt.
/// </summary>
public sealed class SecureTokenStore : ITokenStore
{
    private static readonly string TokenDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ElshazlyStore");

    private static readonly string TokenFile = Path.Combine(TokenDir, "tokens.dat");

    private readonly ILogger<SecureTokenStore> _logger;
    private readonly object _lock = new();

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime? _expiresAtUtc;

    public SecureTokenStore(ILogger<SecureTokenStore> logger)
    {
        _logger = logger;
        Load();
    }

    public string? AccessToken => _accessToken;
    public string? RefreshToken => _refreshToken;
    public DateTime? ExpiresAtUtc => _expiresAtUtc;
    public bool IsExpired => _expiresAtUtc.HasValue && DateTime.UtcNow >= _expiresAtUtc.Value;

    public void SetTokens(string accessToken, string refreshToken, DateTime expiresAtUtc)
    {
        lock (_lock)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _expiresAtUtc = expiresAtUtc;
            Save();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _accessToken = null;
            _refreshToken = null;
            _expiresAtUtc = null;

            try
            {
                if (File.Exists(TokenFile))
                    File.Delete(TokenFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete token file");
            }
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(TokenDir);

            var payload = new TokenPayload
            {
                AccessToken = _accessToken,
                RefreshToken = _refreshToken,
                ExpiresAtUtc = _expiresAtUtc
            };

            var json = JsonSerializer.Serialize(payload);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(TokenFile, encryptedBytes);
            _logger.LogDebug("Tokens saved securely");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save tokens");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(TokenFile)) return;

            var encryptedBytes = File.ReadAllBytes(TokenFile);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);

            var payload = JsonSerializer.Deserialize<TokenPayload>(json);
            if (payload is not null)
            {
                _accessToken = payload.AccessToken;
                _refreshToken = payload.RefreshToken;
                _expiresAtUtc = payload.ExpiresAtUtc;
            }

            _logger.LogDebug("Tokens loaded from secure store");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load tokens from secure store, starting fresh");
            Clear();
        }
    }

    private sealed class TokenPayload
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }
}

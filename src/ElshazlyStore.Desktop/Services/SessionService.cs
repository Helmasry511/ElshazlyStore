using ElshazlyStore.Desktop.Models.Auth;
using ElshazlyStore.Desktop.Services.Api;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.Services;

public sealed class SessionService : ISessionService
{
    private readonly ApiClient _apiClient;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<SessionService> _logger;

    private MeResponse? _currentUser;
    private List<string> _permissions = [];

    public SessionService(ApiClient apiClient, ITokenStore tokenStore, ILogger<SessionService> logger)
    {
        _apiClient = apiClient;
        _tokenStore = tokenStore;
        _logger = logger;

        // Subscribe to session expiry from TokenRefreshHandler
        TokenRefreshHandler.SessionExpired += OnSessionExpired;
    }

    public MeResponse? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser is not null;
    public IReadOnlyList<string> Permissions => _permissions;

    public event Action? SessionEnded;
    public event Action? SessionStarted;

    public async Task<string?> LoginAsync(string username, string password)
    {
        var request = new LoginRequest(username, password);
        var result = await _apiClient.PostAsync<LoginResponse>("/api/v1/auth/login", request);

        if (!result.IsSuccess || result.Data is null)
        {
            _logger.LogWarning("Login failed: {Error}", result.ErrorMessage);
            return result.ErrorMessage ?? "Login failed.";
        }

        // Store tokens
        _tokenStore.SetTokens(result.Data.AccessToken, result.Data.RefreshToken, result.Data.ExpiresAtUtc);
        _logger.LogInformation("Login successful, token expires at {ExpiresAt}", result.Data.ExpiresAtUtc);

        // Fetch user info + permissions
        var loaded = await LoadUserInfoAsync();
        if (!loaded)
        {
            _tokenStore.Clear();
            return "Failed to load user profile after login.";
        }

        SessionStarted?.Invoke();
        return null; // success
    }

    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = _tokenStore.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var request = new LogoutRequest(refreshToken);
                await _apiClient.PostAsync<object>("/api/v1/auth/logout", request);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout API call failed — clearing local session anyway");
        }
        finally
        {
            ClearSession();
        }
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        if (string.IsNullOrEmpty(_tokenStore.AccessToken))
        {
            _logger.LogDebug("No stored token — cannot restore session");
            return false;
        }

        // If the access token is expired but we have a refresh token,
        // the TokenRefreshHandler will handle it on the next API call.
        var loaded = await LoadUserInfoAsync();
        if (!loaded)
        {
            _logger.LogDebug("Session restore failed — clearing tokens");
            _tokenStore.Clear();
            return false;
        }

        _logger.LogInformation("Session restored for user {Username}", _currentUser?.Username);
        SessionStarted?.Invoke();
        return true;
    }

    private async Task<bool> LoadUserInfoAsync()
    {
        var result = await _apiClient.GetAsync<MeResponse>("/api/v1/auth/me");
        if (!result.IsSuccess || result.Data is null)
        {
            _logger.LogWarning("Failed to load user info: {Error}", result.ErrorMessage);
            return false;
        }

        _currentUser = result.Data;

        // Permissions come from /auth/me roles — but we actually need the
        // permission claims from the JWT. For now we fetch them via the me endpoint.
        // The backend puts permissions in the JWT claims, but we parse them separately here.
        await LoadPermissionsAsync();
        return true;
    }

    private Task LoadPermissionsAsync()
    {
        // Parse permission claims from the JWT access token
        var accessToken = _tokenStore.AccessToken;
        if (string.IsNullOrEmpty(accessToken))
        {
            _permissions = [];
            return Task.CompletedTask;
        }

        try
        {
            _permissions = JwtClaimParser.ExtractPermissions(accessToken);
            _logger.LogDebug("Loaded {Count} permissions from JWT", _permissions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JWT permissions");
            _permissions = [];
        }

        return Task.CompletedTask;
    }

    private void ClearSession()
    {
        _currentUser = null;
        _permissions = [];
        _tokenStore.Clear();
        SessionEnded?.Invoke();
    }

    private void OnSessionExpired()
    {
        _logger.LogWarning("Session expired via TokenRefreshHandler");
        _currentUser = null;
        _permissions = [];
        SessionEnded?.Invoke();
    }
}

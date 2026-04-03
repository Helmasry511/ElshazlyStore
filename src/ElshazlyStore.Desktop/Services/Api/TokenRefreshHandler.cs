using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ElshazlyStore.Desktop.Models.Auth;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.Services.Api;

/// <summary>
/// Intercepts 401 responses and attempts to refresh the token once.
/// Uses a semaphore to prevent multiple concurrent refresh attempts.
/// If refresh fails, raises <see cref="SessionExpired"/> event so the UI can redirect to login.
/// </summary>
public sealed class TokenRefreshHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenRefreshHandler> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>
    /// Raised when token refresh fails — the session is expired and user must re-login.
    /// </summary>
    public static event Action? SessionExpired;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TokenRefreshHandler(
        ITokenStore tokenStore,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenRefreshHandler> logger)
    {
        _tokenStore = tokenStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // Only intercept 401 if we have a refresh token to try
        if (response.StatusCode != HttpStatusCode.Unauthorized
            || string.IsNullOrEmpty(_tokenStore.RefreshToken))
        {
            return response;
        }

        // Don't try to refresh if this IS the refresh call (avoid loop)
        if (request.RequestUri?.AbsolutePath.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase) == true)
        {
            return response;
        }

        _logger.LogDebug("Received 401 — attempting token refresh");

        var refreshed = await TryRefreshTokenAsync(cancellationToken);
        if (!refreshed)
        {
            _logger.LogWarning("Token refresh failed — session expired");
            SessionExpired?.Invoke();
            return response;
        }

        // Retry the original request with new token
        response.Dispose();

        var retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

        _logger.LogDebug("Retrying original request with refreshed token");
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken ct)
    {
        // Only one refresh at a time; other threads wait for the result
        await _refreshLock.WaitAsync(ct);
        try
        {
            // Check if another thread already refreshed
            if (!_tokenStore.IsExpired && !string.IsNullOrEmpty(_tokenStore.AccessToken))
            {
                _logger.LogDebug("Token already refreshed by another thread");
                return true;
            }

            var refreshToken = _tokenStore.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
                return false;

            // Use a raw HttpClient (no auth handlers) to call refresh
            using var client = _httpClientFactory.CreateClient("AuthRefresh");
            var body = new RefreshRequest(refreshToken);
            var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", body, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Refresh endpoint returned {StatusCode}", (int)response.StatusCode);
                _tokenStore.Clear();
                return false;
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct);
            if (loginResponse is null || string.IsNullOrEmpty(loginResponse.AccessToken))
            {
                _tokenStore.Clear();
                return false;
            }

            _tokenStore.SetTokens(loginResponse.AccessToken, loginResponse.RefreshToken, loginResponse.ExpiresAtUtc);
            _logger.LogInformation("Token refreshed successfully, expires at {ExpiresAt}", loginResponse.ExpiresAtUtc);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during token refresh");
            _tokenStore.Clear();
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Clones an HttpRequestMessage since they can't be sent twice.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content is not null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            if (original.Content.Headers.ContentType is not null)
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Integration tests for authentication endpoints: login, refresh, me, and permission checks.
/// </summary>
[Collection("Integration")]
public sealed class AuthEndpointTests
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "WrongPassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonexistentUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "nobody", password = "test" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyFields_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUserInfo()
    {
        var token = await LoginAsAdminAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("admin", body.Username);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokens()
    {
        // Login first
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);

        // Refresh
        var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = loginBody!.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        Assert.NotNull(refreshBody);
        Assert.NotEqual(loginBody.RefreshToken, refreshBody.RefreshToken);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = "invalid-token-value" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_DbNeverContainsRawRefreshToken()
    {
        // Login to get a raw refresh token
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        Assert.NotNull(loginBody);
        Assert.False(string.IsNullOrWhiteSpace(loginBody!.RefreshToken));

        var rawToken = loginBody.RefreshToken;

        // Query the DB — no row should hold the raw token value
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hasRawToken = await db.RefreshTokens
            .AnyAsync(rt => rt.TokenHash == rawToken);
        Assert.False(hasRawToken, "DB must not store the raw refresh token as the hash.");

        // Extra: verify the raw token string is not present anywhere in TokenHash or ReplacedByTokenHash
        var allTokens = await db.RefreshTokens
            .Select(rt => new { rt.TokenHash, rt.ReplacedByTokenHash })
            .ToListAsync();

        foreach (var t in allTokens)
        {
            Assert.NotEqual(rawToken, t.TokenHash);
            if (t.ReplacedByTokenHash is not null)
                Assert.NotEqual(rawToken, t.ReplacedByTokenHash);
        }
    }

    [Fact]
    public async Task Refresh_DbNeverContainsRawTokenAfterRotation()
    {
        // Login
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        var rawToken1 = loginBody!.RefreshToken;

        // Refresh to rotate
        var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = rawToken1 });
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        var rawToken2 = refreshBody!.RefreshToken;

        // Neither raw token should be stored in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var allTokens = await db.RefreshTokens
            .Select(rt => new { rt.TokenHash, rt.ReplacedByTokenHash })
            .ToListAsync();

        foreach (var t in allTokens)
        {
            Assert.NotEqual(rawToken1, t.TokenHash);
            Assert.NotEqual(rawToken2, t.TokenHash);
            if (t.ReplacedByTokenHash is not null)
            {
                Assert.NotEqual(rawToken1, t.ReplacedByTokenHash);
                Assert.NotEqual(rawToken2, t.ReplacedByTokenHash);
            }
        }
    }

    [Fact]
    public async Task AuditLog_NeverContainsPasswordHash()
    {
        // Create a user (which triggers INSERT audit on User entity)
        var token = await LoginAsAdminAsync();
        var uniqueUsername = $"audit-test-{Guid.NewGuid():N}"[..20];
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(new
            {
                username = uniqueUsername,
                password = "TestPass@123!",
                roleIds = Array.Empty<Guid>()
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResp = await _client.SendAsync(request);
        // Accept either 200 or 201
        Assert.True(createResp.IsSuccessStatusCode,
            $"User creation failed: {createResp.StatusCode} - {await createResp.Content.ReadAsStringAsync()}");

        // Check audit logs — none should contain "PasswordHash"
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userAudits = await db.AuditLogs
            .Where(a => a.EntityName == "User")
            .ToListAsync();

        foreach (var audit in userAudits)
        {
            if (audit.OldValues is not null)
                Assert.DoesNotContain("PasswordHash", audit.OldValues, StringComparison.OrdinalIgnoreCase);
            if (audit.NewValues is not null)
                Assert.DoesNotContain("PasswordHash", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        }
    }

    // --- Helper ---
    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record MeResp(Guid Id, string Username, bool IsActive, List<string> Roles);
}

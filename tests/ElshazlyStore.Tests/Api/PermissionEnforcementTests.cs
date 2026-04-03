using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests that permission-based authorization blocks unauthenticated/unauthorized access
/// to admin endpoints, and allows access with a valid admin token.
/// </summary>
[Collection("Integration")]
public sealed class PermissionEnforcementTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PermissionEnforcementTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/roles")]
    public async Task AdminEndpoints_WithoutToken_Returns401(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_WithAdminToken_ReturnsOk()
    {
        var token = await LoginAsAdminAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListRoles_WithAdminToken_ReturnsOk()
    {
        var token = await LoginAsAdminAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListPermissions_WithAdminToken_ReturnsOk()
    {
        var token = await LoginAsAdminAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/roles/permissions/all");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithAdminToken_Succeeds()
    {
        var token = await LoginAsAdminAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            username = $"testuser_{Guid.NewGuid():N}".Substring(0, 20),
            password = "Test@123!",
            roleIds = (List<Guid>?)null
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
}

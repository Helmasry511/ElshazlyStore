using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for Phase 9: Printing Policy endpoints.
/// Covers:
/// - Profile CRUD
/// - Rule CRUD (nested under profile)
/// - Policy lookup by screen code
/// - Permission enforcement (MANAGE_PRINTING_POLICY)
/// - Duplicate name / screen code handling
/// - Default profile toggle
/// - Cascade delete
/// </summary>
[Collection("Integration")]
public sealed class PrintingPolicyTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PrintingPolicyTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ───── Permission Tests ─────

    [Fact]
    public async Task PrintProfile_RequiresManagePrintingPolicyPermission()
    {
        // User with only PRODUCTS_READ should be forbidden
        var (limitedToken, _) = await CreateLimitedUserAsync("PRODUCTS_READ");

        var resp1 = await GetAuth("/api/v1/print-profiles", limitedToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp1.StatusCode);

        var resp2 = await PostAuth("/api/v1/print-profiles", limitedToken, new { name = "test" });
        Assert.Equal(HttpStatusCode.Forbidden, resp2.StatusCode);

        var resp3 = await GetAuth("/api/v1/print-policy/SALES_INVOICE", limitedToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp3.StatusCode);
    }

    [Fact]
    public async Task PrintProfile_WithPermission_Allowed()
    {
        var (token, _) = await CreateLimitedUserAsync("MANAGE_PRINTING_POLICY");

        var resp = await GetAuth("/api/v1/print-profiles", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ───── Profile CRUD Tests ─────

    [Fact]
    public async Task CreateProfile_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var name = $"TestProfile-{Guid.NewGuid():N}";

        var resp = await PostAuth("/api/v1/print-profiles", token, new { name, isDefault = false });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ProfileDto>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(name, body!.Name);
        Assert.False(body.IsDefault);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task CreateProfile_DuplicateName_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var name = $"DupProfile-{Guid.NewGuid():N}";

        var resp1 = await PostAuth("/api/v1/print-profiles", token, new { name });
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        var resp2 = await PostAuth("/api/v1/print-profiles", token, new { name });
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_EmptyName_Returns400()
    {
        var token = await LoginAsAdminAsync();

        var resp = await PostAuth("/api/v1/print-profiles", token, new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetProfile_ReturnsProfileWithRules()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"GetProfile-{Guid.NewGuid():N}");

        // Add a rule
        await CreateRuleAsync(token, profileId, "SALES_INVOICE", "{\"header\":true}");

        var resp = await GetAuth($"/api/v1/print-profiles/{profileId}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ProfileDetailDto>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(profileId, body!.Id);
        Assert.Single(body.Rules);
        Assert.Equal("SALES_INVOICE", body.Rules[0].ScreenCode);
    }

    [Fact]
    public async Task GetProfile_NotFound_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var resp = await GetAuth($"/api/v1/print-profiles/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndDefault()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"UpdProfile-{Guid.NewGuid():N}");

        var newName = $"Updated-{Guid.NewGuid():N}";
        var resp = await PutAuth($"/api/v1/print-profiles/{profileId}", token,
            new { name = newName, isDefault = true });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Verify
        var getResp = await GetAuth($"/api/v1/print-profiles/{profileId}", token);
        var body = await getResp.Content.ReadFromJsonAsync<ProfileDetailDto>(JsonOpts);
        Assert.Equal(newName, body!.Name);
        Assert.True(body.IsDefault);
    }

    [Fact]
    public async Task DeleteProfile_RemovesProfile()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"DelProfile-{Guid.NewGuid():N}");

        var resp = await DeleteAuth($"/api/v1/print-profiles/{profileId}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Verify deleted
        var getResp = await GetAuth($"/api/v1/print-profiles/{profileId}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task DeleteProfile_CascadeDeletesRules()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"CascadeProfile-{Guid.NewGuid():N}");
        await CreateRuleAsync(token, profileId, "RECEIPT_PRINT", "{}");
        await CreateRuleAsync(token, profileId, "BARCODE_LABEL", "{}");

        // Delete profile — rules should cascade
        var resp = await DeleteAuth($"/api/v1/print-profiles/{profileId}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Rules endpoint should return empty
        var rulesResp = await GetAuth($"/api/v1/print-profiles/{profileId}/rules", token);
        Assert.Equal(HttpStatusCode.OK, rulesResp.StatusCode);
        var rulesBody = await rulesResp.Content.ReadFromJsonAsync<PagedRulesResp>(JsonOpts);
        Assert.Equal(0, rulesBody!.TotalCount);
    }

    [Fact]
    public async Task ListProfiles_SearchByName()
    {
        var token = await LoginAsAdminAsync();
        var uniqueName = $"SearchProfile-{Guid.NewGuid():N}";
        await CreateProfileAsync(token, uniqueName);

        var resp = await GetAuth($"/api/v1/print-profiles?q={uniqueName}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<PagedProfilesResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 1);
        Assert.Contains(body.Items, p => p.Name == uniqueName);
    }

    // ───── Rule CRUD Tests ─────

    [Fact]
    public async Task CreateRule_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"RuleProfile-{Guid.NewGuid():N}");

        var resp = await PostAuth($"/api/v1/print-profiles/{profileId}/rules", token,
            new { screenCode = "PURCHASE_RECEIPT", configJson = "{\"margins\":{\"top\":10}}", enabled = true });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<RuleDto>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("PURCHASE_RECEIPT", body!.ScreenCode);
        Assert.Contains("margins", body.ConfigJson);
        Assert.True(body.Enabled);
    }

    [Fact]
    public async Task CreateRule_DuplicateScreen_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"DupRuleProfile-{Guid.NewGuid():N}");

        var resp1 = await PostAuth($"/api/v1/print-profiles/{profileId}/rules", token,
            new { screenCode = "INVOICE", configJson = "{}" });
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        var resp2 = await PostAuth($"/api/v1/print-profiles/{profileId}/rules", token,
            new { screenCode = "INVOICE", configJson = "{}" });
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task CreateRule_InvalidProfile_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var resp = await PostAuth($"/api/v1/print-profiles/{Guid.NewGuid()}/rules", token,
            new { screenCode = "TEST", configJson = "{}" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateRule_ChangesConfig()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"UpdRuleProfile-{Guid.NewGuid():N}");
        var ruleId = await CreateRuleAsync(token, profileId, "DELIVERY_NOTE", "{\"old\":true}");

        var resp = await PutAuth($"/api/v1/print-profiles/{profileId}/rules/{ruleId}", token,
            new { configJson = "{\"updated\":true}", enabled = false });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Verify
        var getResp = await GetAuth($"/api/v1/print-profiles/{profileId}/rules/{ruleId}", token);
        var body = await getResp.Content.ReadFromJsonAsync<RuleDto>(JsonOpts);
        Assert.Contains("updated", body!.ConfigJson);
        Assert.False(body.Enabled);
    }

    [Fact]
    public async Task DeleteRule_RemovesRule()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"DelRuleProfile-{Guid.NewGuid():N}");
        var ruleId = await CreateRuleAsync(token, profileId, "STOCK_REPORT", "{}");

        var resp = await DeleteAuth($"/api/v1/print-profiles/{profileId}/rules/{ruleId}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var getResp = await GetAuth($"/api/v1/print-profiles/{profileId}/rules/{ruleId}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    // ───── Policy Lookup Tests ─────

    [Fact]
    public async Task PolicyLookup_DefaultProfile_ReturnsRule()
    {
        var token = await LoginAsAdminAsync();

        // Create a default profile with a rule
        var name = $"DefaultPolicy-{Guid.NewGuid():N}";
        var resp = await PostAuth("/api/v1/print-profiles", token, new { name, isDefault = true });
        resp.EnsureSuccessStatusCode();
        var profile = await resp.Content.ReadFromJsonAsync<ProfileDto>(JsonOpts);

        await CreateRuleAsync(token, profile!.Id, "POS_RECEIPT",
            "{\"paperWidth\":80,\"showLogo\":true}");

        // Lookup without profileId — should use default
        var policyResp = await GetAuth("/api/v1/print-policy/POS_RECEIPT", token);
        Assert.Equal(HttpStatusCode.OK, policyResp.StatusCode);

        var rule = await policyResp.Content.ReadFromJsonAsync<RuleDto>(JsonOpts);
        Assert.NotNull(rule);
        Assert.Equal("POS_RECEIPT", rule!.ScreenCode);
        Assert.Contains("paperWidth", rule.ConfigJson);
    }

    [Fact]
    public async Task PolicyLookup_SpecificProfile_ReturnsRule()
    {
        var token = await LoginAsAdminAsync();
        var profileId = await CreateProfileAsync(token, $"SpecificPolicy-{Guid.NewGuid():N}");
        var screenCode = $"CUSTOM_SCREEN_{Guid.NewGuid():N}";
        await CreateRuleAsync(token, profileId, screenCode, "{\"custom\":true}");

        var policyResp = await GetAuth($"/api/v1/print-policy/{screenCode}?profileId={profileId}", token);
        Assert.Equal(HttpStatusCode.OK, policyResp.StatusCode);

        var rule = await policyResp.Content.ReadFromJsonAsync<RuleDto>(JsonOpts);
        Assert.NotNull(rule);
        Assert.Contains("custom", rule!.ConfigJson);
    }

    [Fact]
    public async Task PolicyLookup_NoMatchingRule_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var resp = await GetAuth($"/api/v1/print-policy/NON_EXISTENT_SCREEN_{Guid.NewGuid():N}", token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ───── Default Profile Toggle ─────

    [Fact]
    public async Task SetDefaultProfile_UnsetsOtherDefaults()
    {
        var token = await LoginAsAdminAsync();

        // Create two profiles, both default
        var name1 = $"Default1-{Guid.NewGuid():N}";
        var name2 = $"Default2-{Guid.NewGuid():N}";

        var resp1 = await PostAuth("/api/v1/print-profiles", token, new { name = name1, isDefault = true });
        resp1.EnsureSuccessStatusCode();
        var profile1 = await resp1.Content.ReadFromJsonAsync<ProfileDto>(JsonOpts);

        var resp2 = await PostAuth("/api/v1/print-profiles", token, new { name = name2, isDefault = true });
        resp2.EnsureSuccessStatusCode();

        // Profile1 should no longer be default
        var getResp = await GetAuth($"/api/v1/print-profiles/{profile1!.Id}", token);
        var body = await getResp.Content.ReadFromJsonAsync<ProfileDetailDto>(JsonOpts);
        Assert.False(body!.IsDefault);
    }

    // ───── Helpers ─────

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private async Task<(string Token, Guid RoleId)> CreateLimitedUserAsync(params string[] permissions)
    {
        var adminToken = await LoginAsAdminAsync();

        var roleResp = await PostAuth("/api/v1/roles", adminToken,
            new { name = $"PrintLimited-{Guid.NewGuid():N}", description = "Limited" });
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.Content.ReadFromJsonAsync<RoleResp>(JsonOpts);

        await PutAuth($"/api/v1/roles/{role!.Id}/permissions", adminToken,
            new { permissionCodes = permissions });

        var username = $"printlim-{Guid.NewGuid():N}";
        await PostAuth("/api/v1/users", adminToken,
            new { username, password = "Limited@123!", roleIds = new[] { role.Id } });

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "Limited@123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);

        return (loginBody!.AccessToken, role.Id);
    }

    private async Task<Guid> CreateProfileAsync(string token, string name)
    {
        var resp = await PostAuth("/api/v1/print-profiles", token, new { name });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ProfileDto>(JsonOpts);
        return body!.Id;
    }

    private async Task<Guid> CreateRuleAsync(string token, Guid profileId, string screenCode, string configJson)
    {
        var resp = await PostAuth($"/api/v1/print-profiles/{profileId}/rules", token,
            new { screenCode, configJson, enabled = true });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<RuleDto>(JsonOpts);
        return body!.Id;
    }

    private async Task<HttpResponseMessage> PostAuth(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAuth(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PutAuth(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> DeleteAuth(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    // ── Response DTOs ──

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record RoleResp(Guid Id, string Name);
    private sealed record ProfileDto(Guid Id, string Name, bool IsDefault, bool IsActive,
        DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, Guid? CreatedByUserId);
    private sealed record ProfileDetailDto(Guid Id, string Name, bool IsDefault, bool IsActive,
        DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, Guid? CreatedByUserId,
        List<RuleDto> Rules);
    private sealed record RuleDto(Guid Id, Guid PrintProfileId, string ScreenCode, string ConfigJson,
        bool Enabled, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, Guid? CreatedByUserId);
    private sealed record PagedProfilesResp(List<ProfileDto> Items, int TotalCount, int Page, int PageSize);
    private sealed record PagedRulesResp(List<RuleDto> Items, int TotalCount, int Page, int PageSize);
}

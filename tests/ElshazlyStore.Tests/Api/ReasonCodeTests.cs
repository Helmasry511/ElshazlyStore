using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for Phase RET 0: Reason Codes catalog.
/// Covers:
/// - CRUD operations (create, read, update, disable)
/// - Code uniqueness constraint
/// - Disable prevents deletion and keeps history
/// - Permission enforcement (MANAGE_REASON_CODES vs VIEW_REASON_CODES)
/// - Category filtering
/// - Seeded default reason codes
/// </summary>
[Collection("Integration")]
public sealed class ReasonCodeTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ReasonCodeTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ───── Permission Tests ─────

    [Fact]
    public async Task ReasonCodes_RequiresViewPermission()
    {
        // User with only PRODUCTS_READ cannot view reason codes
        var (limitedToken, _) = await CreateLimitedUserAsync("PRODUCTS_READ");

        var resp = await GetAuth("/api/v1/reasons", limitedToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ReasonCodes_ViewPermission_AllowsList()
    {
        var (token, _) = await CreateLimitedUserAsync("VIEW_REASON_CODES");

        var resp = await GetAuth("/api/v1/reasons", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ReasonCodes_ViewPermission_CannotCreate()
    {
        var (token, _) = await CreateLimitedUserAsync("VIEW_REASON_CODES");

        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code = "TEST_FORBIDDEN",
            nameAr = "اختبار",
            category = "General"
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ReasonCodes_ManagePermission_CanCreate()
    {
        var (token, _) = await CreateLimitedUserAsync("MANAGE_REASON_CODES");

        var code = $"MGMT_TEST_{Guid.NewGuid():N}"[..30];
        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr = "اختبار إنشاء",
            category = "General"
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    // ───── CRUD Tests ─────

    [Fact]
    public async Task Create_ReturnsCreated_WithCorrectFields()
    {
        var token = await LoginAsAdminAsync();
        var code = $"CRUD_{Guid.NewGuid():N}"[..30];

        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr = "سبب اختبار",
            description = "Test reason",
            category = "SalesReturn",
            requiresManagerApproval = true
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ReasonCodeDto>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(code.ToUpperInvariant(), body!.Code);
        Assert.Equal("سبب اختبار", body.NameAr);
        Assert.Equal("SalesReturn", body.Category);
        Assert.True(body.RequiresManagerApproval);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateCode_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var code = $"DUP_{Guid.NewGuid():N}"[..30];

        var resp1 = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr = "أول",
            category = "General"
        });
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        var resp2 = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr = "ثاني",
            category = "General"
        });
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task Create_EmptyCode_Returns400()
    {
        var token = await LoginAsAdminAsync();

        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code = "",
            nameAr = "تجربة",
            category = "General"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidCategory_Returns400()
    {
        var token = await LoginAsAdminAsync();

        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code = $"BADCAT_{Guid.NewGuid():N}"[..30],
            nameAr = "كاتجوري خطأ",
            category = "InvalidCategory"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsCorrectReason()
    {
        var token = await LoginAsAdminAsync();
        var id = await CreateReasonAsync(token, $"GET_{Guid.NewGuid():N}"[..30], "عرض", "General");

        var resp = await GetAuth($"/api/v1/reasons/{id}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ReasonCodeDto>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(id, body!.Id);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var resp = await GetAuth($"/api/v1/reasons/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var token = await LoginAsAdminAsync();
        var code = $"UPD_{Guid.NewGuid():N}"[..30];
        var id = await CreateReasonAsync(token, code, "قبل", "General");

        var resp = await PutAuth($"/api/v1/reasons/{id}", token, new
        {
            nameAr = "بعد التعديل",
            requiresManagerApproval = true
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Verify
        var getResp = await GetAuth($"/api/v1/reasons/{id}", token);
        var body = await getResp.Content.ReadFromJsonAsync<ReasonCodeDto>(JsonOpts);
        Assert.Equal("بعد التعديل", body!.NameAr);
        Assert.True(body.RequiresManagerApproval);
    }

    // ───── Disable (soft-delete) Tests ─────

    [Fact]
    public async Task Disable_SetsIsActiveFalse_KeepsHistory()
    {
        var token = await LoginAsAdminAsync();
        var code = $"DIS_{Guid.NewGuid():N}"[..30];
        var id = await CreateReasonAsync(token, code, "سيتم تعطيله", "Disposition");

        // Disable
        var disableResp = await PostAuth($"/api/v1/reasons/{id}/disable", token, new { });
        Assert.Equal(HttpStatusCode.OK, disableResp.StatusCode);

        // Verify it still exists but is inactive
        var getResp = await GetAuth($"/api/v1/reasons/{id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var body = await getResp.Content.ReadFromJsonAsync<ReasonCodeDto>(JsonOpts);
        Assert.False(body!.IsActive);
        Assert.Equal(code.ToUpperInvariant(), body.Code); // still accessible
    }

    [Fact]
    public async Task Disable_NotFound_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var resp = await PostAuth($"/api/v1/reasons/{Guid.NewGuid()}/disable", token, new { });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ───── Category Filter Tests ─────

    [Fact]
    public async Task List_FilterByCategory_ReturnsOnlyMatching()
    {
        var token = await LoginAsAdminAsync();

        // Create one in each category
        var codeS = $"FCAT_S_{Guid.NewGuid():N}"[..30];
        var codeP = $"FCAT_P_{Guid.NewGuid():N}"[..30];
        await CreateReasonAsync(token, codeS, "مبيعات", "SalesReturn");
        await CreateReasonAsync(token, codeP, "مشتريات", "PurchaseReturn");

        // Filter SalesReturn
        var resp = await GetAuth("/api/v1/reasons?category=SalesReturn", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<PagedReasonCodesResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.All(body!.Items, item => Assert.Equal("SalesReturn", item.Category));
    }

    // ───── Seeded Data Tests ─────

    [Fact]
    public async Task SeededReasons_ExistInCatalog()
    {
        var token = await LoginAsAdminAsync();

        var resp = await GetAuth("/api/v1/reasons?pageSize=100", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<PagedReasonCodesResp>(JsonOpts);
        Assert.NotNull(body);

        var codes = body!.Items.Select(i => i.Code).ToHashSet();
        // Verify a subset of seeded codes exist
        Assert.Contains("DAMAGED", codes);
        Assert.Contains("THEFT", codes);
        Assert.Contains("CUSTOMER_CHANGED_MIND", codes);
        Assert.Contains("WRONG_ITEM", codes);
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
            new { name = $"RCLimited-{Guid.NewGuid():N}", description = "Limited" });
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.Content.ReadFromJsonAsync<RoleResp>(JsonOpts);

        await PutAuth($"/api/v1/roles/{role!.Id}/permissions", adminToken,
            new { permissionCodes = permissions });

        var username = $"rclim-{Guid.NewGuid():N}";
        await PostAuth("/api/v1/users", adminToken,
            new { username, password = "Limited@123!", roleIds = new[] { role.Id } });

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "Limited@123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);

        return (loginBody!.AccessToken, role.Id);
    }

    private async Task<Guid> CreateReasonAsync(string token, string code, string nameAr, string category)
    {
        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr,
            category
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ReasonCodeDto>(JsonOpts);
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

    // ── Response DTOs ──

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record RoleResp(Guid Id, string Name);
    private sealed record ReasonCodeDto(Guid Id, string Code, string NameAr, string? Description,
        string Category, bool IsActive, bool RequiresManagerApproval,
        DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
    private sealed record PagedReasonCodesResp(List<ReasonCodeDto> Items, int TotalCount, int Page, int PageSize);
}

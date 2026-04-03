using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for import preview (error detection) and commit (permission enforcement).
/// </summary>
[Collection("Integration")]
public sealed class ImportTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ImportTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preview_ProductsCsv_DetectsErrors()
    {
        var token = await LoginAsAdminAsync();

        // CSV with errors: missing product name on row 3, and duplicate SKU on row 4
        var csv = "ProductName,SKU,Barcode,Color,Size,RetailPrice,WholesalePrice\n"
                + $"Product A,SKU-IMP-{Guid.NewGuid():N},BC-IMP-{Guid.NewGuid():N},Red,M,100,80\n"
                + $",SKU-IMP-{Guid.NewGuid():N},BC-IMP-{Guid.NewGuid():N},Blue,L,200,150\n";

        var response = await UploadCsv("/api/v1/imports/masterdata/preview?type=Products", token, csv, "test.csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PreviewResult>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRows);

        // Row 2 (second data row, missing ProductName) should have errors
        Assert.True(result.RowErrors.Count >= 2);
        var row2Errors = result.RowErrors[1]; // second row (0-indexed)
        Assert.True(row2Errors.Count > 0, "Row 2 should have a validation error for missing ProductName.");

        // Row 1 should be valid
        var row1Errors = result.RowErrors[0];
        Assert.Empty(row1Errors);
    }

    [Fact]
    public async Task Preview_CustomersCsv_DetectsDuplicateCode()
    {
        var token = await LoginAsAdminAsync();

        var csv = "Name,Code,Phone,Phone2,Notes\n"
                + "Customer A,CUSTDUP1,01234,,\n"
                + "Customer B,CUSTDUP1,05678,,\n";

        var response = await UploadCsv("/api/v1/imports/masterdata/preview?type=Customers", token, csv, "customers.csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PreviewResult>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRows);

        // Second row should have a duplicate code error
        var row2Errors = result.RowErrors[1];
        Assert.True(row2Errors.Count > 0, "Second row should have duplicate code error.");
    }

    [Fact]
    public async Task Preview_DetectsDuplicateBarcode()
    {
        var token = await LoginAsAdminAsync();
        var dupBarcode = $"DUPBC-{Guid.NewGuid():N}";

        var csv = "ProductName,SKU,Barcode,Color,Size,RetailPrice,WholesalePrice\n"
                + $"Product X,SKU-DBC1-{Guid.NewGuid():N},{dupBarcode},Red,M,100,80\n"
                + $"Product Y,SKU-DBC2-{Guid.NewGuid():N},{dupBarcode},Blue,L,200,150\n";

        var response = await UploadCsv("/api/v1/imports/masterdata/preview?type=Products", token, csv, "test2.csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PreviewResult>(JsonOpts);
        Assert.NotNull(result);

        // Second row should have duplicate barcode error
        var row2Errors = result.RowErrors[1];
        Assert.True(row2Errors.Count > 0, "Second row should have duplicate barcode error.");
    }

    [Fact]
    public async Task Commit_ValidProductsCsv_Succeeds()
    {
        var token = await LoginAsAdminAsync();

        var csv = "ProductName,SKU,Barcode,Color,Size,RetailPrice,WholesalePrice\n"
                + $"Import Product 1,SKU-COM-{Guid.NewGuid():N},BC-COM-{Guid.NewGuid():N},Red,M,100,80\n";

        // Preview
        var previewResp = await UploadCsv("/api/v1/imports/masterdata/preview?type=Products", token, csv, "commit.csv");
        var preview = await previewResp.Content.ReadFromJsonAsync<PreviewResult>(JsonOpts);
        Assert.NotNull(preview);
        Assert.Equal(1, preview.ValidRows);

        // Commit
        var commitResp = await PostAuth("/api/v1/imports/masterdata/commit", token,
            new { jobId = preview.JobId });
        Assert.Equal(HttpStatusCode.OK, commitResp.StatusCode);
    }

    [Fact]
    public async Task Commit_RequiresImportMasterDataPermission()
    {
        // Create a user without IMPORT_MASTER_DATA permission
        var adminToken = await LoginAsAdminAsync();

        // Create a role with only PRODUCTS_READ
        var roleResp = await PostAuth("/api/v1/roles", adminToken,
            new { name = $"LimitedRole-{Guid.NewGuid():N}", description = "No import" });
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.Content.ReadFromJsonAsync<RoleResp>(JsonOpts);

        // Assign only PRODUCTS_READ permission
        var setPermsResp = await PutAuth($"/api/v1/roles/{role!.Id}/permissions", adminToken,
            new { permissionCodes = new[] { "PRODUCTS_READ" } });
        setPermsResp.EnsureSuccessStatusCode();

        // Create user with this limited role
        var username = $"limited-{Guid.NewGuid():N}";
        var userResp = await PostAuth("/api/v1/users", adminToken,
            new { username, password = "Limited@123!", roleIds = new[] { role.Id } });
        userResp.EnsureSuccessStatusCode();

        // Login as limited user
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "Limited@123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        var limitedToken = loginBody!.AccessToken;

        // Try commit — should be forbidden
        var commitResp = await PostAuth("/api/v1/imports/masterdata/commit", limitedToken,
            new { jobId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, commitResp.StatusCode);
    }

    [Fact]
    public async Task Commit_PreviewAlso_RequiresPermission()
    {
        // Create user without import permission
        var adminToken = await LoginAsAdminAsync();

        var roleResp = await PostAuth("/api/v1/roles", adminToken,
            new { name = $"NoImport-{Guid.NewGuid():N}" });
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.Content.ReadFromJsonAsync<RoleResp>(JsonOpts);

        await PutAuth($"/api/v1/roles/{role!.Id}/permissions", adminToken,
            new { permissionCodes = new[] { "PRODUCTS_READ" } });

        var username = $"noimport-{Guid.NewGuid():N}";
        await PostAuth("/api/v1/users", adminToken,
            new { username, password = "NoImport@123!", roleIds = new[] { role.Id } });

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "NoImport@123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);

        var csv = "ProductName,SKU,Barcode\nTest,SKU1,BC1\n";
        var previewResp = await UploadCsv("/api/v1/imports/masterdata/preview?type=Products",
            loginBody!.AccessToken, csv, "test.csv");

        Assert.Equal(HttpStatusCode.Forbidden, previewResp.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private async Task<HttpResponseMessage> UploadCsv(string url, string token, string csvContent, string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
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

    private async Task<HttpResponseMessage> PutAuth(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record PreviewResult(Guid JobId, int TotalRows, int ValidRows, List<List<RowError>> RowErrors);
    private sealed record RowError(string Column, string Message);
    private sealed record RoleResp(Guid Id, string Name);
}

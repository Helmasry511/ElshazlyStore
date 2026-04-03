using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for barcode uniqueness, reservation, and retire behavior.
/// </summary>
[Collection("Integration")]
public sealed class BarcodeTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BarcodeTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateVariant_WithBarcode_AssignsBarcode()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "BarcodeTest Product 1");

        var response = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"BC-SKU-{Guid.NewGuid():N}",
            barcode = $"BC-{Guid.NewGuid():N}",
            color = "Red",
            size = "M",
            retailPrice = 99.99m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateVariant_DuplicateBarcode_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "BarcodeTest Product 2");
        var barcode = $"DUP-{Guid.NewGuid():N}";

        // First variant with this barcode should succeed
        var r1 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"DUP-SKU1-{Guid.NewGuid():N}",
            barcode,
            color = "Blue"
        });
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        // Second variant with same barcode should fail
        var r2 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"DUP-SKU2-{Guid.NewGuid():N}",
            barcode,
            color = "Green"
        });
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task DeleteVariant_RetiresBarcode_CannotReuse()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "BarcodeTest Product 3");
        var barcode = $"RET-{Guid.NewGuid():N}";

        // Create variant
        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"RET-SKU-{Guid.NewGuid():N}",
            barcode,
            color = "White"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);

        // Delete variant (retires barcode)
        var deleteResp = await DeleteAuth($"/api/v1/variants/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        // Try to reuse same barcode — should be rejected
        var reuseResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"RET-SKU2-{Guid.NewGuid():N}",
            barcode,
            color = "Black"
        });
        Assert.Equal(HttpStatusCode.Conflict, reuseResp.StatusCode);
    }

    [Fact]
    public async Task BarcodeLookup_ExistingBarcode_ReturnsProductInfo()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "BarcodeTest Product 4");
        var barcode = $"LOOK-{Guid.NewGuid():N}";

        await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"LOOK-SKU-{Guid.NewGuid():N}",
            barcode,
            color = "Red",
            retailPrice = 49.99m
        });

        var lookupResp = await GetAuth($"/api/v1/barcodes/{barcode}", token);
        Assert.Equal(HttpStatusCode.OK, lookupResp.StatusCode);

        var lookup = await lookupResp.Content.ReadFromJsonAsync<BarcodeLookupResp>(JsonOpts);
        Assert.NotNull(lookup);
        Assert.Equal(barcode, lookup.Barcode);
        Assert.Equal("BarcodeTest Product 4", lookup.ProductName);
    }

    [Fact]
    public async Task BarcodeLookup_NonExistent_Returns404()
    {
        var token = await LoginAsAdminAsync();
        var response = await GetAuth("/api/v1/barcodes/NONEXIST-999", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BarcodeLookup_RetiredBarcode_Returns410()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "BarcodeTest Product 5");
        var barcode = $"RET410-{Guid.NewGuid():N}";

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"RET410-SKU-{Guid.NewGuid():N}",
            barcode,
        });
        var created = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);

        // Delete variant to retire barcode
        await DeleteAuth($"/api/v1/variants/{created!.Id}", token);

        // Lookup should return 410 Gone
        var lookupResp = await GetAuth($"/api/v1/barcodes/{barcode}", token);
        Assert.Equal(HttpStatusCode.Gone, lookupResp.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private async Task<Guid> CreateProductAsync(string token, string name)
    {
        var resp = await PostAuth("/api/v1/products", token, new { name });
        resp.EnsureSuccessStatusCode();
        var product = await resp.Content.ReadFromJsonAsync<ProductResp>(JsonOpts);
        return product!.Id;
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

    private async Task<HttpResponseMessage> DeleteAuth(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record ProductResp(Guid Id, string Name);
    private sealed record VariantResp(Guid Id, Guid ProductId, string Sku, string? Barcode);
    private sealed record BarcodeLookupResp(string Barcode, string Status,
        Guid VariantId, string Sku, string ProductName);
}

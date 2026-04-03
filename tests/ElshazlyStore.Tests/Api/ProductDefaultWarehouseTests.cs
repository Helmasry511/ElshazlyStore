using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for BACKEND 3 — Default Warehouse metadata on Product.
/// Verifies create/update with DefaultWarehouseId, validation of invalid/inactive warehouse,
/// variant DTO read-only warehouse info, and absence of stock quantity fields.
/// </summary>
[Collection("Integration")]
public sealed class ProductDefaultWarehouseTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProductDefaultWarehouseTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ──────────────────────────────────────────────────────────────
    // 1. Create product WITH DefaultWarehouseId → persisted and returned in GET
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateProduct_WithDefaultWarehouseId_PersistedAndReturned()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Create product with default warehouse
        var createResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-Create",
            description = "Test product with default warehouse",
            category = "Test",
            defaultWarehouseId = warehouseId
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<ProductDtoResp>(JsonOpts);
        Assert.NotNull(created);
        Assert.Equal(warehouseId, created!.DefaultWarehouseId);
        Assert.False(string.IsNullOrEmpty(created.DefaultWarehouseName));

        // GET the product and verify
        var getResp = await GetAuth($"/api/v1/products/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var detail = await getResp.Content.ReadFromJsonAsync<ProductDetailDtoResp>(JsonOpts);
        Assert.NotNull(detail);
        Assert.Equal(warehouseId, detail!.DefaultWarehouseId);
        Assert.False(string.IsNullOrEmpty(detail.DefaultWarehouseName));
    }

    // ──────────────────────────────────────────────────────────────
    // 2. Create product WITHOUT DefaultWarehouseId → null by default
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateProduct_WithoutDefaultWarehouse_NullByDefault()
    {
        var token = await LoginAsAdminAsync();

        var createResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-NoWarehouse",
            description = "No default warehouse"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<ProductDtoResp>(JsonOpts);
        Assert.NotNull(created);
        Assert.Null(created!.DefaultWarehouseId);
        Assert.Null(created.DefaultWarehouseName);
    }

    // ──────────────────────────────────────────────────────────────
    // 3. Update product DefaultWarehouseId → reflected
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task UpdateProduct_ChangeDefaultWarehouse_Reflected()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Create product without warehouse
        var createResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-Update"
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ProductDtoResp>(JsonOpts);

        // Update to set warehouse
        var updateResp = await PutAuth($"/api/v1/products/{created!.Id}", token, new
        {
            defaultWarehouseId = warehouseId
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Verify
        var getResp = await GetAuth($"/api/v1/products/{created.Id}", token);
        var detail = await getResp.Content.ReadFromJsonAsync<ProductDetailDtoResp>(JsonOpts);
        Assert.Equal(warehouseId, detail!.DefaultWarehouseId);
    }

    // ──────────────────────────────────────────────────────────────
    // 4. Update product — clear DefaultWarehouseId with Guid.Empty
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task UpdateProduct_ClearDefaultWarehouse_SetsNull()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Create product with warehouse
        var createResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-Clear",
            defaultWarehouseId = warehouseId
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ProductDtoResp>(JsonOpts);

        // Clear warehouse by sending Guid.Empty
        var updateResp = await PutAuth($"/api/v1/products/{created!.Id}", token, new
        {
            defaultWarehouseId = Guid.Empty
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Verify it's null now
        var getResp = await GetAuth($"/api/v1/products/{created.Id}", token);
        var detail = await getResp.Content.ReadFromJsonAsync<ProductDetailDtoResp>(JsonOpts);
        Assert.Null(detail!.DefaultWarehouseId);
        Assert.Null(detail.DefaultWarehouseName);
    }

    // ──────────────────────────────────────────────────────────────
    // 5. Create product with INVALID warehouseId → error
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreateProduct_InvalidWarehouseId_ReturnsNotFound()
    {
        var token = await LoginAsAdminAsync();

        var createResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-InvalidWh",
            defaultWarehouseId = Guid.NewGuid() // nonexistent
        });

        Assert.Equal(HttpStatusCode.NotFound, createResp.StatusCode);
        var body = await createResp.Content.ReadAsStringAsync();
        Assert.Contains("WAREHOUSE_NOT_FOUND", body);
    }

    // ──────────────────────────────────────────────────────────────
    // 6. Variant DTO includes read-only default warehouse info from product
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task VariantDto_IncludesReadOnlyDefaultWarehouseFromProduct()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Create product with warehouse
        var createProdResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-VariantCheck",
            defaultWarehouseId = warehouseId
        });
        createProdResp.EnsureSuccessStatusCode();
        var product = await createProdResp.Content.ReadFromJsonAsync<ProductDtoResp>(JsonOpts);

        // Create variant
        var createVarResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"DW-VAR-{Guid.NewGuid():N}",
            barcode = $"DW-BC-{Guid.NewGuid():N}",
            color = "Blue",
            size = "M"
        });
        createVarResp.EnsureSuccessStatusCode();
        var variant = await createVarResp.Content.ReadFromJsonAsync<VariantListDtoResp>(JsonOpts);
        Assert.NotNull(variant);
        Assert.Equal(warehouseId, variant!.DefaultWarehouseId);
        Assert.False(string.IsNullOrEmpty(variant.DefaultWarehouseName));

        // Also verify via GET variant by ID
        var getVarResp = await GetAuth($"/api/v1/variants/{variant.Id}", token);
        getVarResp.EnsureSuccessStatusCode();
        var varDetail = await getVarResp.Content.ReadFromJsonAsync<VariantListDtoResp>(JsonOpts);
        Assert.Equal(warehouseId, varDetail!.DefaultWarehouseId);
        Assert.False(string.IsNullOrEmpty(varDetail.DefaultWarehouseName));
    }

    // ──────────────────────────────────────────────────────────────
    // 7. Variant DTO for product without warehouse → null fields
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task VariantDto_ProductWithoutWarehouse_NullFields()
    {
        var token = await LoginAsAdminAsync();

        // Create product without warehouse
        var createProdResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-VariantNoWh"
        });
        createProdResp.EnsureSuccessStatusCode();
        var product = await createProdResp.Content.ReadFromJsonAsync<ProductDtoResp>(JsonOpts);

        // Create variant
        var createVarResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"DW-NW-{Guid.NewGuid():N}",
            barcode = $"DW-NW-BC-{Guid.NewGuid():N}"
        });
        createVarResp.EnsureSuccessStatusCode();
        var variant = await createVarResp.Content.ReadFromJsonAsync<VariantListDtoResp>(JsonOpts);
        Assert.Null(variant!.DefaultWarehouseId);
        Assert.Null(variant.DefaultWarehouseName);
    }

    // ──────────────────────────────────────────────────────────────
    // 8. Product list includes DefaultWarehouseId and Name
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task ProductList_IncludesDefaultWarehouseFields()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Create product with warehouse
        await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-List-Check",
            defaultWarehouseId = warehouseId
        });

        // List products
        var listResp = await GetAuth("/api/v1/products?q=DW-Test-List-Check", token);
        listResp.EnsureSuccessStatusCode();
        var list = await listResp.Content.ReadFromJsonAsync<PagedProductResp>(JsonOpts);
        Assert.NotNull(list);
        var item = list!.Items.FirstOrDefault(p => p.DefaultWarehouseId == warehouseId);
        Assert.NotNull(item);
        Assert.False(string.IsNullOrEmpty(item!.DefaultWarehouseName));
    }

    // ──────────────────────────────────────────────────────────────
    // 9. Static check: no stock quantity fields introduced on Product or Variant
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public void NoStockQuantityFieldsOnProduct()
    {
        var productType = typeof(ElshazlyStore.Domain.Entities.Product);
        var props = productType.GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Quantity", props);
        Assert.DoesNotContain("Qty", props);
        Assert.DoesNotContain("StockQuantity", props);
        Assert.DoesNotContain("Stock", props);
    }

    [Fact]
    public void NoStockQuantityFieldsOnVariant()
    {
        var variantType = typeof(ElshazlyStore.Domain.Entities.ProductVariant);
        var props = variantType.GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Quantity", props);
        Assert.DoesNotContain("Qty", props);
        Assert.DoesNotContain("StockQuantity", props);
        Assert.DoesNotContain("Stock", props);
    }

    // ──────────────────────────────────────────────────────────────
    // 10. ProductDetail GET includes variant DTOs with default warehouse info
    // ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task ProductDetail_VariantsIncludeDefaultWarehouseInfo()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Create product with warehouse
        var createProdResp = await PostAuth("/api/v1/products", token, new
        {
            name = "DW-Test-DetailVariants",
            defaultWarehouseId = warehouseId
        });
        createProdResp.EnsureSuccessStatusCode();
        var product = await createProdResp.Content.ReadFromJsonAsync<ProductDtoResp>(JsonOpts);

        // Create a variant
        var createVarResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"DW-DV-{Guid.NewGuid():N}",
            barcode = $"DW-DV-BC-{Guid.NewGuid():N}",
            color = "Red"
        });
        createVarResp.EnsureSuccessStatusCode();

        // GET product detail — check variants in the detail response
        var getResp = await GetAuth($"/api/v1/products/{product.Id}", token);
        getResp.EnsureSuccessStatusCode();
        var detail = await getResp.Content.ReadFromJsonAsync<ProductDetailDtoResp>(JsonOpts);
        Assert.NotEmpty(detail!.Variants);
        foreach (var v in detail.Variants)
        {
            Assert.Equal(warehouseId, v.DefaultWarehouseId);
            Assert.False(string.IsNullOrEmpty(v.DefaultWarehouseName));
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private async Task<Guid> GetDefaultWarehouseIdAsync(string token)
    {
        var resp = await GetAuth("/api/v1/warehouses", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedWarehouseResp>(JsonOpts);
        return body!.Items.First(w => w.IsDefault).Id;
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

    private async Task<HttpResponseMessage> GetAuth(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    // ── Response DTOs ──

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record ProductDtoResp(Guid Id, string Name, string? Description, string? Category,
        bool IsActive, DateTime CreatedAtUtc, int VariantCount,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName);
    private sealed record ProductDetailDtoResp(Guid Id, string Name, string? Description, string? Category,
        bool IsActive, DateTime CreatedAtUtc,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName,
        List<VariantInDetailResp> Variants);
    private sealed record VariantInDetailResp(Guid Id, Guid ProductId, string Sku, string? Color, string? Size,
        decimal? RetailPrice, decimal? WholesalePrice, bool IsActive, string? Barcode,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName);
    private sealed record VariantListDtoResp(Guid Id, Guid ProductId, string ProductName,
        string Sku, string? Color, string? Size, decimal? RetailPrice,
        decimal? WholesalePrice, bool IsActive, string? Barcode,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName);
    private sealed record PagedProductResp(List<ProductDtoResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault, bool IsActive);
}

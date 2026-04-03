using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for server-side SKU and Barcode auto-generation on variant create.
/// </summary>
[Collection("Integration")]
public sealed class IdentifierGenerationTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public IdentifierGenerationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateVariant_OmitSkuAndBarcode_ServerGeneratesBoth()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "AutoGen Product 1");

        var response = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            color = "Red",
            size = "L",
            retailPrice = 29.99m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var variant = await response.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);

        // SKU should be 10-digit numeric
        Assert.False(string.IsNullOrWhiteSpace(variant.Sku), "Server should have generated a SKU.");
        Assert.Matches(@"^\d{10}$", variant.Sku);

        // Barcode should be 13-digit numeric
        Assert.False(string.IsNullOrWhiteSpace(variant.Barcode), "Server should have generated a Barcode.");
        Assert.Matches(@"^\d{13}$", variant.Barcode);
    }

    [Fact]
    public async Task CreateVariant_ExplicitSkuAndBarcode_PreservesValues()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "AutoGen Product 2");

        var explicitSku = $"EXPLICIT-{Guid.NewGuid():N}"[..20];
        var explicitBarcode = $"EBC-{Guid.NewGuid():N}";

        var response = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = explicitSku,
            barcode = explicitBarcode,
            color = "Blue"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var variant = await response.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);
        Assert.Equal(explicitSku, variant.Sku);
        Assert.Equal(explicitBarcode, variant.Barcode);
    }

    [Fact]
    public async Task CreateVariant_OmitSkuOnly_GeneratesSkuKeepsBarcode()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "AutoGen Product 3");
        var explicitBarcode = $"SKUGEN-{Guid.NewGuid():N}";

        var response = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            barcode = explicitBarcode,
            color = "Green"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var variant = await response.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);
        Assert.Matches(@"^\d{10}$", variant!.Sku);
        Assert.Equal(explicitBarcode, variant.Barcode);
    }

    [Fact]
    public async Task CreateVariant_OmitBarcodeOnly_GeneratesBarcodeKeepsSku()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "AutoGen Product 4");
        var explicitSku = $"BCGEN-{Guid.NewGuid():N}"[..20];

        var response = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = explicitSku,
            color = "Yellow"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var variant = await response.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);
        Assert.Equal(explicitSku, variant!.Sku);
        Assert.Matches(@"^\d{13}$", variant.Barcode);
    }

    [Fact]
    public async Task CreateVariant_MultipleOmitted_GeneratesUniqueIdentifiers()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "AutoGen Product 5");

        var variants = new List<VariantResp>();
        for (var i = 0; i < 5; i++)
        {
            var response = await PostAuth("/api/v1/variants", token, new
            {
                productId,
                color = $"Color{i}",
                size = $"Size{i}"
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var v = await response.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
            Assert.NotNull(v);
            variants.Add(v!);
        }

        // All SKUs should be unique
        var skus = variants.Select(v => v.Sku).Distinct().ToList();
        Assert.Equal(5, skus.Count);

        // All barcodes should be unique
        var barcodes = variants.Select(v => v.Barcode).Distinct().ToList();
        Assert.Equal(5, barcodes.Count);
    }

    [Fact]
    public async Task CreateVariant_DuplicateExplicitSku_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "AutoGen Product 6");
        var sku = $"DUPSKU-{Guid.NewGuid():N}"[..20];

        var r1 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku,
            barcode = $"BC1-{Guid.NewGuid():N}",
            color = "Black"
        });
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku,
            barcode = $"BC2-{Guid.NewGuid():N}",
            color = "White"
        });
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task CreateVariant_GeneratedSkuAppearsInBarcodeLookup()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "AutoGen Product 7");

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            color = "Silver",
            retailPrice = 15.00m
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);

        // Lookup by the generated barcode should return the generated SKU
        var lookupResp = await GetAuth($"/api/v1/barcodes/{variant!.Barcode}", token);
        Assert.Equal(HttpStatusCode.OK, lookupResp.StatusCode);

        var lookup = await lookupResp.Content.ReadFromJsonAsync<BarcodeLookupResp>(JsonOpts);
        Assert.NotNull(lookup);
        Assert.Equal(variant.Sku, lookup!.Sku);
        Assert.Equal(variant.Barcode, lookup.Barcode);
    }

    // ── R1 — Discoverability Tests ──────────────────────────

    [Fact]
    public async Task ManualBarcode_AppearsInBarcodeLookup()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "R1 Manual BC Lookup");
        var explicitBarcode = $"R1BC-{Guid.NewGuid():N}";

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            barcode = explicitBarcode,
            color = "Red"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);
        Assert.Equal(explicitBarcode, variant!.Barcode);

        // Manual barcode must be discoverable
        var lookupResp = await GetAuth($"/api/v1/barcodes/{explicitBarcode}", token);
        Assert.Equal(HttpStatusCode.OK, lookupResp.StatusCode);
        var lookup = await lookupResp.Content.ReadFromJsonAsync<BarcodeLookupResp>(JsonOpts);
        Assert.NotNull(lookup);
        Assert.Equal(explicitBarcode, lookup!.Barcode);
        Assert.Equal(variant.Sku, lookup.Sku);
    }

    [Fact]
    public async Task ManualSku_DiscoverableViaBySkuEndpoint()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "R1 Manual SKU BySku");
        var explicitSku = $"R1SKU-{Guid.NewGuid():N}"[..20];

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = explicitSku,
            color = "Blue"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);
        Assert.Equal(explicitSku, variant!.Sku);

        // Manual SKU must be discoverable via by-sku endpoint
        var lookupResp = await GetAuth($"/api/v1/variants/by-sku/{explicitSku}", token);
        Assert.Equal(HttpStatusCode.OK, lookupResp.StatusCode);
        var lookup = await lookupResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(lookup);
        Assert.Equal(explicitSku, lookup!.Sku);
        Assert.Equal(variant.Id, lookup.Id);
    }

    [Fact]
    public async Task GeneratedSku_DiscoverableViaBySkuEndpoint()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "R1 GenSku BySku");

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            color = "Green"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);
        Assert.Matches(@"^\d{10}$", variant!.Sku);

        // Generated SKU must be discoverable via by-sku endpoint
        var lookupResp = await GetAuth($"/api/v1/variants/by-sku/{variant.Sku}", token);
        Assert.Equal(HttpStatusCode.OK, lookupResp.StatusCode);
        var lookup = await lookupResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(lookup);
        Assert.Equal(variant.Sku, lookup!.Sku);
        Assert.Equal(variant.Id, lookup.Id);
    }

    [Fact]
    public async Task ManualSku_DiscoverableViaListSearch()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "R1 Manual SKU Search");
        var explicitSku = $"SRCH-{Guid.NewGuid():N}"[..20];

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = explicitSku,
            color = "Orange"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        // Must be discoverable via list search
        var searchResp = await GetAuth($"/api/v1/variants?q={explicitSku}", token);
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        var body = await searchResp.Content.ReadAsStringAsync();
        Assert.Contains(explicitSku, body);
    }

    [Fact]
    public async Task GeneratedSku_DiscoverableViaListSearch()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "R1 GenSku Search");

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            color = "Yellow"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);

        // Generated SKU must be discoverable via list search
        var searchResp = await GetAuth($"/api/v1/variants?q={variant!.Sku}", token);
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        var body = await searchResp.Content.ReadAsStringAsync();
        Assert.Contains(variant.Sku, body);
    }

    [Fact]
    public async Task BySkuEndpoint_NonExistent_Returns404()
    {
        var token = await LoginAsAdminAsync();

        var lookupResp = await GetAuth("/api/v1/variants/by-sku/DOES-NOT-EXIST-999", token);
        Assert.Equal(HttpStatusCode.NotFound, lookupResp.StatusCode);
    }

    [Fact]
    public async Task DuplicateExplicitBarcode_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "R1 Dup Barcode");
        var sharedBarcode = $"R1DUP-{Guid.NewGuid():N}";

        var r1 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            barcode = sharedBarcode,
            color = "Black"
        });
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            barcode = sharedBarcode,
            color = "White"
        });
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    // ── POLICY LOCK — Barcode Immutability + SKU Update Tests ──────

    [Fact]
    public async Task UpdateVariant_ChangeBarcode_Returns409_BarcodeImmutable()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "PLock Barcode Immut");

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            barcode = $"IMMUT-{Guid.NewGuid():N}",
            color = "Red"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);

        // Attempt to change barcode — must be rejected
        var updateResp = await PutAuth($"/api/v1/variants/{variant!.Id}", token, new
        {
            barcode = "DIFFERENT-BARCODE-VALUE"
        });
        Assert.Equal(HttpStatusCode.Conflict, updateResp.StatusCode);
        var body = await updateResp.Content.ReadAsStringAsync();
        Assert.Contains("BARCODE_IMMUTABLE", body);
    }

    [Fact]
    public async Task UpdateVariant_SameBarcode_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "PLock Same BC");
        var barcode = $"SAME-{Guid.NewGuid():N}";

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            barcode,
            color = "Blue"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);

        // Sending same barcode back should be fine (no-op)
        var updateResp = await PutAuth($"/api/v1/variants/{variant!.Id}", token, new
        {
            barcode,
            color = "Green"
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
    }

    [Fact]
    public async Task UpdateVariant_OmitBarcode_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "PLock No BC");

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            color = "Orange"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);

        // Update without barcode field — should succeed
        var updateResp = await PutAuth($"/api/v1/variants/{variant!.Id}", token, new
        {
            color = "Purple"
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
    }

    [Fact]
    public async Task UpdateVariant_ChangeSkuToExisting_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "PLock SKU Conflict");
        var sku1 = $"UPSKU1-{Guid.NewGuid():N}"[..20];
        var sku2 = $"UPSKU2-{Guid.NewGuid():N}"[..20];

        // Create two variants with distinct SKUs
        var r1 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = sku1,
            barcode = $"BC1-{Guid.NewGuid():N}",
            color = "Black"
        });
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = sku2,
            barcode = $"BC2-{Guid.NewGuid():N}",
            color = "White"
        });
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        var variant2 = await r2.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant2);

        // Attempt to change variant2's SKU to sku1 (already taken)
        var updateResp = await PutAuth($"/api/v1/variants/{variant2!.Id}", token, new
        {
            sku = sku1
        });
        Assert.Equal(HttpStatusCode.Conflict, updateResp.StatusCode);
    }

    [Fact]
    public async Task UpdateVariant_ChangeSkuToNewUnique_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var productId = await CreateProductAsync(token, "PLock SKU OK");

        var createResp = await PostAuth("/api/v1/variants", token, new
        {
            productId,
            sku = $"CHGSKU-{Guid.NewGuid():N}"[..20],
            color = "Gray"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var variant = await createResp.Content.ReadFromJsonAsync<VariantResp>(JsonOpts);
        Assert.NotNull(variant);

        var newSku = $"NEWSKU-{Guid.NewGuid():N}"[..20];
        var updateResp = await PutAuth($"/api/v1/variants/{variant!.Id}", token, new
        {
            sku = newSku
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
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

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record ProductResp(Guid Id, string Name);
    private sealed record VariantResp(Guid Id, Guid ProductId, string Sku, string? Barcode);
    private sealed record BarcodeLookupResp(string Barcode, string Status, Guid VariantId, string Sku);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for purchase receipt CRUD, posting generates stock movement,
/// idempotency, and inventory update verification.
/// </summary>
[Collection("Integration")]
public sealed class PurchaseReceiptTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PurchaseReceiptTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePurchaseReceipt_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Create");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Create");

        var resp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            notes = "Test receipt",
            lines = new[] { new { variantId, quantity = 100m, unitCost = 25.50m } }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("Draft", body!.Status);
        Assert.Single(body.Lines);
        Assert.Equal(100m, body.Lines[0].Quantity);
        Assert.Equal(25.50m, body.Lines[0].UnitCost);
    }

    [Fact]
    public async Task PostPurchaseReceipt_CreatesStockMovementAndUpdatesBalance()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Post");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Post");

        // Create receipt
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 50m, unitCost = 10m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        // Post the receipt
        var postResp = await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var postBody = await postResp.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(postBody);
        Assert.NotEqual(Guid.Empty, postBody!.StockMovementId);

        // Verify stock balance increased
        var balance = await GetBalanceAsync(token, warehouseId);
        var item = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(item);
        Assert.Equal(50m, item!.Quantity);
    }

    [Fact]
    public async Task PostPurchaseReceipt_IsIdempotent()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Idemp");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Idemp");

        // Create and post
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 30m, unitCost = 5m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        var post1 = await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post1.StatusCode);
        var body1 = await post1.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        // Post again — should be idempotent
        var post2 = await PostAuth($"/api/v1/purchases/{receipt.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post2.StatusCode);
        var body2 = await post2.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        Assert.Equal(body1!.StockMovementId, body2!.StockMovementId);

        // Verify balance didn't double
        var balance = await GetBalanceAsync(token, warehouseId);
        var item = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(item);
        Assert.Equal(30m, item!.Quantity);
    }

    [Fact]
    public async Task PostPurchaseReceipt_ConcurrentDoublePost_OnlyOneStockMovement()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Conc");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Conc");

        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 20m, unitCost = 5m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        // Fire two parallel post requests
        var task1 = PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        var task2 = PostAuth($"/api/v1/purchases/{receipt.Id}/post", token, new { });
        var results = await Task.WhenAll(task1, task2);

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(okCount >= 1, $"At least one post must succeed. Statuses: {string.Join(", ", results.Select(r => r.StatusCode))}");

        foreach (var r in results)
        {
            Assert.True(
                r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict,
                $"Expected 200 or 409 but got {(int)r.StatusCode}");
        }

        // Key invariant: stock increased exactly once (0 + 20 = 20)
        var balance = await GetBalanceAsync(token, warehouseId);
        var item = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(item);
        Assert.Equal(20m, item!.Quantity);

        var getResp = await GetAuth($"/api/v1/purchases/{receipt.Id}", token);
        var posted = await getResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);
        Assert.Equal("Posted", posted!.Status);
        Assert.NotNull(posted.StockMovementId);
    }

    [Fact]
    public async Task PostPurchaseReceipt_MultipleLines_UpdatesAllBalances()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Multi");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId1 = await CreateVariantAsync(token, "PurchaseTest-Multi-V1");
        var variantId2 = await CreateVariantAsync(token, "PurchaseTest-Multi-V2");

        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[]
            {
                new { variantId = variantId1, quantity = 100m, unitCost = 10m },
                new { variantId = variantId2, quantity = 200m, unitCost = 20m },
            }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        // Post
        var postResp = await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Verify both balances
        var balance = await GetBalanceAsync(token, warehouseId);
        Assert.Equal(100m, balance.First(b => b.VariantId == variantId1).Quantity);
        Assert.Equal(200m, balance.First(b => b.VariantId == variantId2).Quantity);
    }

    [Fact]
    public async Task GetPurchaseReceipt_ReturnsDetails()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Get");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Get");

        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            documentNumber = $"PR-GET-{Guid.NewGuid():N}",
            notes = "Get test",
            lines = new[] { new { variantId, quantity = 10m, unitCost = 5m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        var getResp = await GetAuth($"/api/v1/purchases/{receipt!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var body = await getResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(receipt.Id, body!.Id);
        Assert.Equal("Get test", body.Notes);
    }

    [Fact]
    public async Task ListPurchaseReceipts_WithSearch()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Search");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Search");

        var docNum = $"PR-SEARCH-{Guid.NewGuid():N}";
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            documentNumber = docNum,
            lines = new[] { new { variantId, quantity = 5m, unitCost = 1m } }
        });
        createResp.EnsureSuccessStatusCode();

        // Search by document number
        var listResp = await GetAuth($"/api/v1/purchases?q={docNum}", token);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<PagedReceiptResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 1);
        Assert.Contains(body.Items, r => r.DocumentNumber == docNum);
    }

    [Fact]
    public async Task UpdatePurchaseReceipt_DraftOnly()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Update");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Update");

        // Create draft
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 10m, unitCost = 5m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        // Update notes
        var updateResp = await PutAuth($"/api/v1/purchases/{receipt!.Id}", token, new
        {
            notes = "Updated notes"
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Post it
        await PostAuth($"/api/v1/purchases/{receipt.Id}/post", token, new { });

        // Attempt to update posted receipt — should fail
        var updateAfterPost = await PutAuth($"/api/v1/purchases/{receipt.Id}", token, new
        {
            notes = "Should fail"
        });
        Assert.Equal(HttpStatusCode.Conflict, updateAfterPost.StatusCode);
    }

    [Fact]
    public async Task DeletePurchaseReceipt_DraftOnly()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-Del");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-Del");

        // Create and delete
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, unitCost = 1m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        var delResp = await DeleteAuth($"/api/v1/purchases/{receipt!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);

        // Verify it's gone
        var getResp = await GetAuth($"/api/v1/purchases/{receipt.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task DeletePurchaseReceipt_PostedFails()
    {
        var token = await LoginAsAdminAsync();
        var supplierId = await CreateSupplierAsync(token, "PurchaseTest-Supplier-DelPost");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-DelPost");

        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, unitCost = 1m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        // Post it
        await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });

        // Delete should fail
        var delResp = await DeleteAuth($"/api/v1/purchases/{receipt.Id}", token);
        Assert.Equal(HttpStatusCode.Conflict, delResp.StatusCode);
    }

    [Fact]
    public async Task CreatePurchaseReceipt_InvalidSupplier_Returns404()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "PurchaseTest-BadSup");

        var resp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId = Guid.NewGuid(),
            warehouseId,
            lines = new[] { new { variantId, quantity = 10m, unitCost = 5m } }
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Purchases_RequireAuthentication()
    {
        var resp = await _client.GetAsync("/api/v1/purchases");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Helpers ──

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private async Task<Guid> CreateSupplierAsync(string token, string name)
    {
        var resp = await PostAuth("/api/v1/suppliers", token, new { name });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<SupplierResp>(JsonOpts);
        return body!.Id;
    }

    private async Task<Guid> CreateVariantAsync(string token, string name)
    {
        var prodResp = await PostAuth("/api/v1/products", token, new { name });
        prodResp.EnsureSuccessStatusCode();
        var product = await prodResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);

        var varResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"PUR-{Guid.NewGuid():N}",
            barcode = $"PUR-BC-{Guid.NewGuid():N}",
            color = "Red",
            size = "M",
            retailPrice = 100m
        });
        varResp.EnsureSuccessStatusCode();
        var variant = await varResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return variant!.Id;
    }

    private async Task<Guid> GetDefaultWarehouseIdAsync(string token)
    {
        var resp = await GetAuth("/api/v1/warehouses", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedWarehouseResp>(JsonOpts);
        return body!.Items.First(w => w.IsDefault).Id;
    }

    private async Task<List<BalanceItemResp>> GetBalanceAsync(string token, Guid warehouseId)
    {
        var resp = await GetAuth($"/api/v1/stock/balances?warehouseId={warehouseId}&pageSize=100", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedBalanceResp>(JsonOpts);
        return body!.Items;
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
    private sealed record IdResp(Guid Id);
    private sealed record SupplierResp(Guid Id, string Code, string Name);
    private sealed record ReceiptResp(
        Guid Id, string DocumentNumber, Guid SupplierId, string SupplierName,
        Guid WarehouseId, string WarehouseName, string? Notes,
        string Status, Guid? StockMovementId,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        List<ReceiptLineResp> Lines);
    private sealed record ReceiptLineResp(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal UnitCost, decimal LineTotal);
    private sealed record PostResultResp(Guid StockMovementId);
    private sealed record PagedReceiptResp(List<ReceiptResp> Items, int TotalCount);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
}

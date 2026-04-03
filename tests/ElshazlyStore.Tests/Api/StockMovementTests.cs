using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for stock movement posting, balance updates, negative stock rejection,
/// and concurrency safety design.
/// </summary>
[Collection("Integration")]
public sealed class StockMovementTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public StockMovementTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostMovement_PurchaseReceipt_UpdatesBalance()
    {
        var token = await LoginAsAdminAsync();
        var (productId, variantId) = await CreateProductAndVariantAsync(token, "StockTest-Purchase");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Post a PurchaseReceipt of 100 units
        var postResult = await PostMovementAsync(token, new
        {
            type = 1, // PurchaseReceipt
            reference = "PO-001",
            notes = "Initial purchase",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 100m, unitCost = 10.50m, reason = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.OK, postResult.StatusCode);
        var body = await postResult.Content.ReadFromJsonAsync<MovementResultResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.MovementId);

        // Verify balance
        var balance = await GetBalanceAsync(token, warehouseId);
        var item = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(item);
        Assert.Equal(100m, item!.Quantity);
    }

    [Fact]
    public async Task PostMovement_SaleIssue_ReducesBalance()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantId) = await CreateProductAndVariantAsync(token, "StockTest-Sale");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // First: add stock via PurchaseReceipt
        await PostMovementAsync(token, new
        {
            type = 1, // PurchaseReceipt
            reference = "PO-002",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 50m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        // Then: issue 20 units via SaleIssue
        var saleResult = await PostMovementAsync(token, new
        {
            type = 2, // SaleIssue
            reference = "SO-001",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = -20m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.OK, saleResult.StatusCode);

        // Verify balance = 50 - 20 = 30
        var balance = await GetBalanceAsync(token, warehouseId);
        var item = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(item);
        Assert.Equal(30m, item!.Quantity);
    }

    [Fact]
    public async Task PostMovement_NegativeStock_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantId) = await CreateProductAndVariantAsync(token, "StockTest-NegReject");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Attempt to issue 10 units with 0 balance
        var result = await PostMovementAsync(token, new
        {
            type = 2, // SaleIssue
            reference = "SO-NEG",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = -10m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync();
        Assert.Contains("STOCK_NEGATIVE_NOT_ALLOWED", body);
    }

    [Fact]
    public async Task PostMovement_Transfer_MovesStock()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantId) = await CreateProductAndVariantAsync(token, "StockTest-Transfer");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Create a second warehouse
        var wh2 = await CreateWarehouseAsync(token, "TRANSFER-WH", "Transfer Test Warehouse");

        // Add stock to default warehouse
        await PostMovementAsync(token, new
        {
            type = 0, // OpeningBalance
            reference = "OB-001",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 200m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        // Transfer 75 units from default to second warehouse
        var transferResult = await PostMovementAsync(token, new
        {
            type = 3, // Transfer
            reference = "TR-001",
            notes = "Transfer to secondary",
            lines = new object[]
            {
                new { variantId, warehouseId, quantityDelta = -75m, unitCost = (decimal?)null, reason = "Transfer out" },
                new { variantId, warehouseId = wh2, quantityDelta = 75m, unitCost = (decimal?)null, reason = "Transfer in" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, transferResult.StatusCode);

        // Verify balances
        var balDef = await GetBalanceAsync(token, warehouseId);
        Assert.Equal(125m, balDef.First(b => b.VariantId == variantId).Quantity);

        var balWh2 = await GetBalanceAsync(token, wh2);
        Assert.Equal(75m, balWh2.First(b => b.VariantId == variantId).Quantity);
    }

    [Fact]
    public async Task PostMovement_Adjustment_Works()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantId) = await CreateProductAndVariantAsync(token, "StockTest-Adjust");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Add initial stock
        await PostMovementAsync(token, new
        {
            type = 0, // OpeningBalance
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 100m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        // Adjustment: +5 (found extra during count)
        var adjResult = await PostMovementAsync(token, new
        {
            type = 4, // Adjustment
            reference = "ADJ-001",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 5m, unitCost = (decimal?)null, reason = "Physical count surplus" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, adjResult.StatusCode);

        var balance = await GetBalanceAsync(token, warehouseId);
        Assert.Equal(105m, balance.First(b => b.VariantId == variantId).Quantity);
    }

    [Fact]
    public async Task PostMovement_EmptyLines_Returns400()
    {
        var token = await LoginAsAdminAsync();

        var result = await PostMovementAsync(token, new
        {
            type = 0,
            lines = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync();
        Assert.Contains("MOVEMENT_EMPTY", body);
    }

    [Fact]
    public async Task PostMovement_InvalidVariant_Returns404()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        var result = await PostMovementAsync(token, new
        {
            type = 0, // OpeningBalance
            lines = new[]
            {
                new { variantId = Guid.NewGuid(), warehouseId, quantityDelta = 10m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync();
        Assert.Contains("VARIANT_NOT_FOUND", body);
    }

    [Fact]
    public async Task Ledger_ShowsMovementHistory()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantId) = await CreateProductAndVariantAsync(token, "StockTest-Ledger");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Post two movements
        await PostMovementAsync(token, new
        {
            type = 0,
            reference = "OB-LEDGER",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 50m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        await PostMovementAsync(token, new
        {
            type = 2, // SaleIssue
            reference = "SO-LEDGER",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = -10m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        // Query ledger
        var resp = await GetAuth($"/api/v1/stock/ledger?variantId={variantId}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<PagedLedgerResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(2, body!.TotalCount);
    }

    [Fact]
    public async Task PostMovement_RequiresStockPostPermission()
    {
        // Without token → 401
        var result = await _client.PostAsJsonAsync("/api/v1/stock-movements/post", new
        {
            type = 0,
            lines = new[] { new { variantId = Guid.NewGuid(), warehouseId = Guid.NewGuid(), quantityDelta = 10m } }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task StockBalances_RequiresStockReadPermission()
    {
        var result = await _client.GetAsync("/api/v1/stock/balances");
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    // ── Transfer Invariant Tests ──

    [Fact]
    public async Task Transfer_SinglePositiveLine_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantId) = await CreateProductAndVariantAsync(token, "StockTest-TransferBad1");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Add stock so the line itself would be valid
        await PostMovementAsync(token, new
        {
            type = 0, // OpeningBalance
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 100m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        // Attempt transfer with only a single positive line => unbalanced
        var result = await PostMovementAsync(token, new
        {
            type = 3, // Transfer
            reference = "TR-BAD-1",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 50m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync();
        Assert.Contains("TRANSFER_UNBALANCED", body);
    }

    [Fact]
    public async Task Transfer_DifferentVariants_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantIdA) = await CreateProductAndVariantAsync(token, "StockTest-TransferBad2A");
        var (_, variantIdB) = await CreateProductAndVariantAsync(token, "StockTest-TransferBad2B");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var wh2 = await CreateWarehouseAsync(token, $"TXBAD2-{Guid.NewGuid():N}"[..12], "Transfer Bad 2");

        // Add stock for variant A
        await PostMovementAsync(token, new
        {
            type = 0,
            lines = new[]
            {
                new { variantId = variantIdA, warehouseId, quantityDelta = 100m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        // Attempt transfer: -50 variant A, +50 variant B => per-variant unbalanced
        var result = await PostMovementAsync(token, new
        {
            type = 3, // Transfer
            reference = "TR-BAD-2",
            lines = new object[]
            {
                new { variantId = variantIdA, warehouseId, quantityDelta = -50m, unitCost = (decimal?)null, reason = (string?)null },
                new { variantId = variantIdB, warehouseId = wh2, quantityDelta = 50m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync();
        Assert.Contains("TRANSFER_UNBALANCED", body);
    }

    [Fact]
    public async Task Transfer_ValidSameVariantTwoWarehouses_Accepted()
    {
        var token = await LoginAsAdminAsync();
        var (_, variantId) = await CreateProductAndVariantAsync(token, "StockTest-TransferOK");
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var wh2 = await CreateWarehouseAsync(token, $"TXOK-{Guid.NewGuid():N}"[..12], "Transfer OK WH");

        // Add stock to source warehouse
        await PostMovementAsync(token, new
        {
            type = 0,
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = 200m, unitCost = (decimal?)null, reason = (string?)null }
            }
        });

        // Valid transfer: -60 from WH1, +60 to WH2 (same variant)
        var result = await PostMovementAsync(token, new
        {
            type = 3,
            reference = "TR-OK-1",
            lines = new object[]
            {
                new { variantId, warehouseId, quantityDelta = -60m, unitCost = (decimal?)null, reason = "Transfer out" },
                new { variantId, warehouseId = wh2, quantityDelta = 60m, unitCost = (decimal?)null, reason = "Transfer in" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        // Verify balances
        var balWh1 = await GetBalanceAsync(token, warehouseId);
        Assert.Equal(140m, balWh1.First(b => b.VariantId == variantId).Quantity);

        var balWh2 = await GetBalanceAsync(token, wh2);
        Assert.Equal(60m, balWh2.First(b => b.VariantId == variantId).Quantity);
    }

    // ── Helpers ──

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private async Task<(Guid ProductId, Guid VariantId)> CreateProductAndVariantAsync(string token, string name)
    {
        var prodResp = await PostAuth("/api/v1/products", token, new { name });
        prodResp.EnsureSuccessStatusCode();
        var product = await prodResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);

        var varResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"STK-{Guid.NewGuid():N}",
            barcode = $"STK-BC-{Guid.NewGuid():N}",
            color = "Black",
            size = "L",
            retailPrice = 50m
        });
        varResp.EnsureSuccessStatusCode();
        var variant = await varResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);

        return (product.Id, variant!.Id);
    }

    private async Task<Guid> GetDefaultWarehouseIdAsync(string token)
    {
        var resp = await GetAuth("/api/v1/warehouses", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedWarehouseResp>(JsonOpts);
        return body!.Items.First(w => w.IsDefault).Id;
    }

    private async Task<Guid> CreateWarehouseAsync(string token, string code, string name)
    {
        var resp = await PostAuth("/api/v1/warehouses", token, new { code, name });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return body!.Id;
    }

    private async Task<HttpResponseMessage> PostMovementAsync(string token, object body)
    {
        return await PostAuth("/api/v1/stock-movements/post", token, body);
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

    // ── Response DTOs ──

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record IdResp(Guid Id);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity, bool IsDefault);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
    private sealed record MovementResultResp(Guid MovementId);
    private sealed record PagedLedgerResp(int TotalCount);
}

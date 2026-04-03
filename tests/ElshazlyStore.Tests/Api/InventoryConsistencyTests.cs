using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// BACKEND 6A — Inventory Consistency: Purchases/Returns must update stock balances.
/// Verifies that posting purchases and purchase returns updates both the ledger
/// AND the balances endpoint atomically.
/// </summary>
[Collection("Integration")]
public sealed class InventoryConsistencyTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public InventoryConsistencyTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─────────────────────────────────────────────────────────────
    //  Test 1 — PostPurchase_UpdatesBalancesAndLedger
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostPurchase_UpdatesBalancesAndLedger()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "IC-PurchBal");
        var variantId = await CreateVariantAsync(token, "IC-PurchBal");

        // Record balance BEFORE posting the purchase
        var balanceBefore = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(0m, balanceBefore);

        // Create and post a purchase receipt for 75 units at 12.50 each
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 75m, unitCost = 12.50m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // ── Verify balances updated ──
        var balanceAfter = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(75m, balanceAfter);

        // ── Verify ledger updated ──
        var ledger = await GetLedgerForVariant(token, variantId, warehouseId);
        Assert.Contains(ledger, e => e.QuantityDelta == 75m && e.Type == 1); // PurchaseReceipt = 1
    }

    // ─────────────────────────────────────────────────────────────
    //  Test 2 — PostPurchaseReturn_SubtractsBalancesAndAddsLedgerEntry
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostPurchaseReturn_SubtractsBalancesAndAddsLedgerEntry()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "IC-RetBal");
        var variantId = await CreateVariantAsync(token, "IC-RetBal");
        var reasonId = await CreateReasonCodeAsync(token, $"IC_RB_{Guid.NewGuid():N}"[..30]);

        // Seed 200 units via opening balance
        await SeedStock(token, variantId, warehouseId, 200m);

        // Verify initial balance
        var balanceBefore = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(200m, balanceBefore);

        // Create and post a purchase return for 35 units
        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId,
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 35m, unitCost = 10m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // ── Verify balance subtracted ──
        var balanceAfter = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(165m, balanceAfter); // 200 - 35

        // ── Verify ledger shows PurchaseReturnIssue entry ──
        var ledger = await GetLedgerForVariant(token, variantId, warehouseId);
        Assert.Contains(ledger, e => e.QuantityDelta == -35m && e.Type == 8); // PurchaseReturnIssue = 8
    }

    // ─────────────────────────────────────────────────────────────
    //  Test 3 — VoidPurchaseReturn_OnlyDraftAllowed (posted stays permanent)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task VoidPurchaseReturn_PostedReturnCannotBeVoided_BalancesUnchanged()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "IC-Void");
        var variantId = await CreateVariantAsync(token, "IC-Void");
        var reasonId = await CreateReasonCodeAsync(token, $"IC_VO_{Guid.NewGuid():N}"[..30]);

        // Seed 100 units
        await SeedStock(token, variantId, warehouseId, 100m);

        // Create and post a purchase return for 20 units
        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId,
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 20m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Verify balance is 80 now (100 - 20)
        var balanceAfterPost = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(80m, balanceAfterPost);

        // Attempt to void the posted return — must be rejected
        var voidResp = await PostAuth($"/api/v1/purchase-returns/{returnDto.Id}/void", token, new { });
        Assert.Equal(HttpStatusCode.Conflict, voidResp.StatusCode);

        // Balance must remain at 80 (no reversal)
        var balanceAfterVoid = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(80m, balanceAfterVoid);
    }

    // ─────────────────────────────────────────────────────────────
    //  Test 4 — Concurrency safety: atomic transaction
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentPurchasePost_BalanceUpdatedExactlyOnce()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "IC-Conc");
        var variantId = await CreateVariantAsync(token, "IC-Conc");

        // Create a single purchase receipt for 60 units
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 60m, unitCost = 5m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        // Fire two parallel post requests (simulate concurrent users)
        var task1 = PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        var task2 = PostAuth($"/api/v1/purchases/{receipt.Id}/post", token, new { });
        var results = await Task.WhenAll(task1, task2);

        // At least one must succeed; the other is OK or 409
        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(okCount >= 1);
        foreach (var r in results)
            Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict,
                $"Expected 200 or 409 but got {(int)r.StatusCode}");

        // Key invariant: balance must be EXACTLY 60 (posted once, no double credit)
        var balance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(60m, balance);

        // Ledger must have exactly one PurchaseReceipt movement
        var ledger = await GetLedgerForVariant(token, variantId, warehouseId);
        Assert.Single(ledger.Where(e => e.Type == 1 && e.QuantityDelta == 60m));
    }

    // ─────────────────────────────────────────────────────────────
    //  Test 5 — Purchase + Return flow: balance consistent throughout
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PurchaseThenReturn_BalancesConsistentThroughout()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "IC-Flow");
        var variantId = await CreateVariantAsync(token, "IC-Flow");
        var reasonId = await CreateReasonCodeAsync(token, $"IC_FL_{Guid.NewGuid():N}"[..30]);

        // Step 1: Balance starts at 0
        Assert.Equal(0m, await GetBalanceForVariant(token, warehouseId, variantId));

        // Step 2: Purchase 50 units
        var createPurch = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 50m, unitCost = 10m } }
        });
        createPurch.EnsureSuccessStatusCode();
        var receipt = await createPurch.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);
        var postPurch = await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        postPurch.EnsureSuccessStatusCode();

        Assert.Equal(50m, await GetBalanceForVariant(token, warehouseId, variantId));

        // Step 3: Return 15 units
        var createRet = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 15m, unitCost = 10m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });
        createRet.EnsureSuccessStatusCode();
        var retDto = await createRet.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        var postRet = await PostAuth($"/api/v1/purchase-returns/{retDto!.Id}/post", token, new { });
        postRet.EnsureSuccessStatusCode();

        Assert.Equal(35m, await GetBalanceForVariant(token, warehouseId, variantId));

        // Step 4: Ledger has exactly 3 entries (opening-0 + purchase-50 + return−15)
        var ledger = await GetLedgerForVariant(token, variantId, warehouseId);
        var purchaseEntries = ledger.Where(e => e.Type == 1).ToList(); // PurchaseReceipt
        var returnEntries = ledger.Where(e => e.Type == 8).ToList(); // PurchaseReturnIssue
        Assert.Single(purchaseEntries);
        Assert.Equal(50m, purchaseEntries[0].QuantityDelta);
        Assert.Single(returnEntries);
        Assert.Equal(-15m, returnEntries[0].QuantityDelta);
    }

    // ─────────────────────────────────────────────────────────────
    //  Test 6 — Ledger-derived balance matches endpoint
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LedgerSum_MatchesBalanceEndpoint()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "IC-Match");
        var variantId = await CreateVariantAsync(token, "IC-Match");
        var reasonId = await CreateReasonCodeAsync(token, $"IC_MA_{Guid.NewGuid():N}"[..30]);

        // Purchase 120 units
        var pur = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 120m, unitCost = 8m } }
        });
        pur.EnsureSuccessStatusCode();
        var receipt = await pur.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);
        (await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { })).EnsureSuccessStatusCode();

        // Return 30 units
        var ret = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 30m, unitCost = 8m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });
        ret.EnsureSuccessStatusCode();
        var retDto = await ret.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        (await PostAuth($"/api/v1/purchase-returns/{retDto!.Id}/post", token, new { })).EnsureSuccessStatusCode();

        // Compute expected balance from ledger
        var ledger = await GetLedgerForVariant(token, variantId, warehouseId);
        var ledgerSum = ledger.Sum(e => e.QuantityDelta);

        // Get balance from the endpoint
        var balance = await GetBalanceForVariant(token, warehouseId, variantId);

        // They MUST be identical
        Assert.Equal(ledgerSum, balance);
        Assert.Equal(90m, balance); // 120 - 30
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

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
            sku = $"IC-{Guid.NewGuid():N}",
            barcode = $"IC-BC-{Guid.NewGuid():N}",
            color = "Blue",
            size = "L",
            retailPrice = 50m
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

    private async Task<Guid> CreateReasonCodeAsync(string token, string code)
    {
        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr = "سبب مرتجع",
            category = "PurchaseReturn"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ReasonResp>(JsonOpts);
        return body!.Id;
    }

    private async Task SeedStock(string token, Guid variantId, Guid warehouseId, decimal qty)
    {
        var resp = await PostAuth("/api/v1/stock-movements/post", token, new
        {
            type = 0,
            reference = $"IC-SEED-{Guid.NewGuid():N}",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = qty, unitCost = (decimal?)1m, reason = (string?)null }
            }
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<decimal> GetBalanceForVariant(string token, Guid warehouseId, Guid variantId)
    {
        var resp = await GetAuth($"/api/v1/stock/balances?warehouseId={warehouseId}&pageSize=200", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedBalanceResp>(JsonOpts);
        var item = body!.Items.FirstOrDefault(b => b.VariantId == variantId);
        return item?.Quantity ?? 0m;
    }

    private async Task<List<LedgerEntryResp>> GetLedgerForVariant(
        string token, Guid variantId, Guid warehouseId)
    {
        var resp = await GetAuth(
            $"/api/v1/stock/ledger?variantId={variantId}&warehouseId={warehouseId}&pageSize=200", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedLedgerResp>(JsonOpts);
        return body!.Items;
    }

    private async Task<HttpResponseMessage> PostAuth(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
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
    private sealed record SupplierResp(Guid Id, string Code, string Name);
    private sealed record ReasonResp(Guid Id, string Code);
    private sealed record ReceiptResp(Guid Id, string DocumentNumber, string Status, List<ReceiptLineResp> Lines);
    private sealed record ReceiptLineResp(Guid VariantId, decimal Quantity, decimal UnitCost);
    private sealed record ReturnResp(
        Guid Id, string ReturnNumber, Guid SupplierId,
        Guid? OriginalPurchaseReceiptId, Guid WarehouseId,
        string Status, Guid? StockMovementId, decimal TotalAmount,
        List<ReturnLineResp> Lines);
    private sealed record ReturnLineResp(Guid VariantId, decimal Quantity, decimal UnitCost);
    private sealed record PostResultResp(Guid? StockMovementId);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record LedgerEntryResp(
        Guid MovementId, int Type, string? Reference,
        DateTime PostedAtUtc, Guid VariantId, string Sku,
        Guid WarehouseId, string WarehouseCode,
        decimal QuantityDelta, decimal? UnitCost, string? Reason);
    private sealed record PagedLedgerResp(List<LedgerEntryResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
}

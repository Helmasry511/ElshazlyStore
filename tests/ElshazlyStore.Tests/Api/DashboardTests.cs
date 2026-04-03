using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for Phase 8: Dashboard KPI endpoints.
/// Covers:
/// - Sales summary (total, count, avg ticket)
/// - Top products by quantity and revenue
/// - Low stock alerts with threshold
/// - Cashier performance
/// - Permission enforcement
/// - Date range filtering
/// </summary>
[Collection("Integration")]
public sealed class DashboardTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DashboardTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ───── Sales Summary Tests ─────

    [Fact]
    public async Task Summary_SalesMetrics_ComputedCorrectly()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v1 = await CreateVariantAsync(token, "Dash-Sales1");
        var v2 = await CreateVariantAsync(token, "Dash-Sales2");

        // Seed stock
        await SeedStock(token, v1, warehouseId, 100m);
        await SeedStock(token, v2, warehouseId, 100m);

        // Create and post 2 invoices
        // Invoice 1: 5 * 20 = 100
        var inv1 = await CreateAndPostInvoice(token, warehouseId, v1, 5m, 20m);
        // Invoice 2: 3 * 30 = 90
        var inv2 = await CreateAndPostInvoice(token, warehouseId, v2, 3m, 30m);

        // Query with wide date range
        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/sales?from={from}&to={to}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var sales = await resp.Content.ReadFromJsonAsync<SalesSummaryResp>(JsonOpts);
        Assert.NotNull(sales);
        // At minimum, includes our 2 invoices (there may be invoices from other tests)
        Assert.True(sales!.InvoiceCount >= 2, $"Expected >=2 invoices, got {sales.InvoiceCount}");
        Assert.True(sales.TotalSales >= 190m, $"Expected >=190 total, got {sales.TotalSales}");
        Assert.True(sales.AverageTicket > 0m, "Average ticket should be positive");
    }

    [Fact]
    public async Task Summary_DateRange_FiltersCorrectly()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v = await CreateVariantAsync(token, "Dash-Range");

        await SeedStock(token, v, warehouseId, 100m);
        await CreateAndPostInvoice(token, warehouseId, v, 2m, 10m);

        // Query with future date range — should return 0 invoices for that range
        var futureFrom = DateTime.UtcNow.AddDays(1).ToString("O");
        var futureTo = DateTime.UtcNow.AddDays(2).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/sales?from={futureFrom}&to={futureTo}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var sales = await resp.Content.ReadFromJsonAsync<SalesSummaryResp>(JsonOpts);
        Assert.Equal(0, sales!.InvoiceCount);
        Assert.Equal(0m, sales.TotalSales);
        Assert.Equal(0m, sales.AverageTicket);
    }

    // ───── Top Products Tests ─────

    [Fact]
    public async Task TopProducts_ByQuantity_OrderedCorrectly()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        var vLow = await CreateVariantAsync(token, "Dash-TopLow");
        var vHigh = await CreateVariantAsync(token, "Dash-TopHigh");

        await SeedStock(token, vLow, warehouseId, 100m);
        await SeedStock(token, vHigh, warehouseId, 100m);

        // vLow: sell 2, vHigh: sell 10
        await CreateAndPostInvoice(token, warehouseId, vLow, 2m, 50m);
        await CreateAndPostInvoice(token, warehouseId, vHigh, 10m, 10m);

        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/top-products?from={from}&to={to}&orderBy=quantity&topN=100", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var products = await resp.Content.ReadFromJsonAsync<List<TopProductResp>>(JsonOpts);
        Assert.NotNull(products);
        Assert.True(products!.Count >= 2);

        // Find our two
        var high = products.FirstOrDefault(p => p.VariantId == vHigh);
        var low = products.FirstOrDefault(p => p.VariantId == vLow);
        Assert.NotNull(high);
        Assert.NotNull(low);
        Assert.Equal(10m, high!.TotalQuantity);
        Assert.Equal(2m, low!.TotalQuantity);

        // vHigh should appear before vLow (higher quantity)
        var highIdx = products.IndexOf(high);
        var lowIdx = products.IndexOf(low!);
        Assert.True(highIdx < lowIdx, "Higher quantity product should come first");
    }

    [Fact]
    public async Task TopProducts_ByRevenue_OrderedCorrectly()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        var vCheap = await CreateVariantAsync(token, "Dash-RevCheap");
        var vExpensive = await CreateVariantAsync(token, "Dash-RevExp");

        await SeedStock(token, vCheap, warehouseId, 100m);
        await SeedStock(token, vExpensive, warehouseId, 100m);

        // vCheap: 10 * 5 = 50 revenue, vExpensive: 2 * 100 = 200 revenue
        await CreateAndPostInvoice(token, warehouseId, vCheap, 10m, 5m);
        await CreateAndPostInvoice(token, warehouseId, vExpensive, 2m, 100m);

        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/top-products?from={from}&to={to}&orderBy=revenue&topN=100", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var products = await resp.Content.ReadFromJsonAsync<List<TopProductResp>>(JsonOpts);
        Assert.NotNull(products);

        var expIdx = products!.FindIndex(p => p.VariantId == vExpensive);
        var cheapIdx = products.FindIndex(p => p.VariantId == vCheap);
        Assert.True(expIdx >= 0, "Expensive variant should be in results");
        Assert.True(cheapIdx >= 0, "Cheap variant should be in results");

        Assert.Equal(200m, products[expIdx].TotalRevenue);
        Assert.Equal(50m, products[cheapIdx].TotalRevenue);

        // Expensive should appear first
        Assert.True(expIdx < cheapIdx, "Higher revenue product should come first");
    }

    // ───── Low Stock Alerts Tests ─────

    [Fact]
    public async Task LowStock_AlertsOnLowQuantity()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v = await CreateVariantAsync(token, "Dash-LowStock");

        // Seed exactly 3 units
        await SeedStock(token, v, warehouseId, 3m);

        // Check low stock with threshold=5 (3 <= 5 → alert)
        var resp = await GetAuth("/api/v1/dashboard/low-stock?threshold=5", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var alerts = await resp.Content.ReadFromJsonAsync<List<LowStockAlertResp>>(JsonOpts);
        Assert.NotNull(alerts);
        var alert = alerts!.FirstOrDefault(a => a.VariantId == v);
        Assert.NotNull(alert);
        Assert.Equal(3m, alert!.CurrentStock);
        Assert.Equal(5m, alert.Threshold);
    }

    [Fact]
    public async Task LowStock_NoAlertAboveThreshold()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v = await CreateVariantAsync(token, "Dash-NoAlert");

        // Seed 100 units
        await SeedStock(token, v, warehouseId, 100m);

        // Check with threshold=5 — 100 > 5, no alert
        var resp = await GetAuth("/api/v1/dashboard/low-stock?threshold=5", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var alerts = await resp.Content.ReadFromJsonAsync<List<LowStockAlertResp>>(JsonOpts);
        var alert = alerts!.FirstOrDefault(a => a.VariantId == v);
        Assert.Null(alert); // should not be in alerts
    }

    [Fact]
    public async Task LowStock_UsesVariantThreshold_WhenSet()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v = await CreateVariantAsync(token, "Dash-VarThresh");

        // Seed 8 units
        await SeedStock(token, v, warehouseId, 8m);

        // Set variant LowStockThreshold to 10 (8 <= 10 → alert even though default threshold=5)
        var updateResp = await PutAuth($"/api/v1/variants/{v}", token,
            new { lowStockThreshold = 10m });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Check with default threshold 5 — variant threshold (10) overrides
        var resp = await GetAuth("/api/v1/dashboard/low-stock?threshold=5", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var alerts = await resp.Content.ReadFromJsonAsync<List<LowStockAlertResp>>(JsonOpts);
        var alert = alerts!.FirstOrDefault(a => a.VariantId == v);
        Assert.NotNull(alert);
        Assert.Equal(8m, alert!.CurrentStock);
        Assert.Equal(10m, alert.Threshold); // uses variant threshold
    }

    // ───── Cashier Performance Tests ─────

    [Fact]
    public async Task CashierPerformance_ReturnsData()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v = await CreateVariantAsync(token, "Dash-Cashier");

        await SeedStock(token, v, warehouseId, 100m);
        await CreateAndPostInvoice(token, warehouseId, v, 5m, 20m);

        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/cashier-performance?from={from}&to={to}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var cashiers = await resp.Content.ReadFromJsonAsync<List<CashierPerfResp>>(JsonOpts);
        Assert.NotNull(cashiers);
        Assert.True(cashiers!.Count >= 1);

        var admin = cashiers.FirstOrDefault(c => c.CashierUsername == "admin");
        Assert.NotNull(admin);
        Assert.True(admin!.InvoiceCount >= 1);
        Assert.True(admin.TotalSales >= 100m);
        Assert.True(admin.AverageTicket > 0m);
    }

    // ───── Full Summary Tests ─────

    [Fact]
    public async Task Summary_ReturnsAllSections()
    {
        var token = await LoginAsAdminAsync();

        var from = DateTime.UtcNow.AddDays(-30).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/summary?from={from}&to={to}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var summary = await resp.Content.ReadFromJsonAsync<DashboardSummaryResp>(JsonOpts);
        Assert.NotNull(summary);
        Assert.NotNull(summary!.Sales);
        Assert.NotNull(summary.TopProductsByQuantity);
        Assert.NotNull(summary.TopProductsByRevenue);
        Assert.NotNull(summary.LowStockAlerts);
        Assert.NotNull(summary.CashierPerformance);
    }

    // ───── Permission Tests ─────

    [Fact]
    public async Task Dashboard_RequiresPermission()
    {
        var (limitedToken, _) = await CreateLimitedUserAsync("PRODUCTS_READ");

        var resp = await GetAuth("/api/v1/dashboard/summary", limitedToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        var resp2 = await GetAuth("/api/v1/dashboard/sales", limitedToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp2.StatusCode);

        var resp3 = await GetAuth("/api/v1/dashboard/low-stock", limitedToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp3.StatusCode);
    }

    [Fact]
    public async Task Dashboard_WithPermission_Allowed()
    {
        var (token, _) = await CreateLimitedUserAsync("DASHBOARD_READ");

        var resp = await GetAuth("/api/v1/dashboard/summary", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ───── Default Date Range Test ─────

    [Fact]
    public async Task Summary_DefaultDateRange_UsesCurrentMonth()
    {
        var token = await LoginAsAdminAsync();

        // No from/to params — should default to current month
        var resp = await GetAuth("/api/v1/dashboard/summary", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var summary = await resp.Content.ReadFromJsonAsync<DashboardSummaryResp>(JsonOpts);
        Assert.NotNull(summary);
        // Just verify it returns without error
        Assert.True(summary!.Sales.InvoiceCount >= 0);
    }

    // ───── RET 4: Net Revenue Tests ─────

    [Fact]
    public async Task SalesSummary_NetSales_SubtractsReturns()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v = await CreateVariantAsync(token, "Dash-NetSales");
        var reasonId = await CreateReasonCodeAsync(token, $"DN_{Guid.NewGuid():N}"[..30]);

        // Seed stock
        await SeedStock(token, v, warehouseId, 200m);

        // Create and post a sales invoice: 10 * 30 = 300
        var invoiceId = await CreateAndPostInvoice(token, warehouseId, v, 10m, 30m);

        // Create and post a sales return: 3 * 30 = 90
        var returnId = await CreateAndPostSalesReturn(token, warehouseId, v, null, null, 3m, 30m, reasonId);

        // Query dashboard
        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/sales?from={from}&to={to}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var sales = await resp.Content.ReadFromJsonAsync<SalesSummaryResp>(JsonOpts);
        Assert.NotNull(sales);

        // Net sales must be at least 300 - 90 = 210 (other test invoices may contribute)
        Assert.True(sales!.TotalSales >= 300m, $"TotalSales should be >=300, got {sales.TotalSales}");
        Assert.True(sales.TotalSalesReturns >= 90m, $"TotalSalesReturns should be >=90, got {sales.TotalSalesReturns}");
        Assert.True(sales.ReturnCount >= 1, "ReturnCount should be >=1");
        Assert.Equal(sales.TotalSales - sales.TotalSalesReturns, sales.NetSales);
        Assert.True(sales.NetSales < sales.TotalSales, "NetSales should be less than TotalSales when returns exist");
    }

    [Fact]
    public async Task TopProducts_NetQuantity_SubtractsReturned()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v = await CreateVariantAsync(token, "Dash-NetQty");
        var reasonId = await CreateReasonCodeAsync(token, $"DQ_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, v, warehouseId, 200m);

        // Sell 10 units at 20 each = 200 revenue
        await CreateAndPostInvoice(token, warehouseId, v, 10m, 20m);

        // Return 4 units at 20 each = 80 returned revenue
        await CreateAndPostSalesReturn(token, warehouseId, v, null, null, 4m, 20m, reasonId);

        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/top-products?from={from}&to={to}&orderBy=quantity&topN=100", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var products = await resp.Content.ReadFromJsonAsync<List<TopProductResp>>(JsonOpts);
        Assert.NotNull(products);

        var product = products!.FirstOrDefault(p => p.VariantId == v);
        Assert.NotNull(product);

        // Verify gross and net quantities
        Assert.Equal(10m, product!.TotalQuantity);
        Assert.Equal(4m, product.ReturnedQuantity);
        Assert.Equal(6m, product.NetQuantity); // 10 - 4

        // Verify gross and net revenue
        Assert.Equal(200m, product.TotalRevenue);
        Assert.Equal(80m, product.ReturnedRevenue);
        Assert.Equal(120m, product.NetRevenue); // 200 - 80
    }

    [Fact]
    public async Task TopProducts_ByRevenue_SortsByNetRevenue()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);

        // Product A: sell high revenue, but return most of it
        var vA = await CreateVariantAsync(token, "Dash-NetOrdA");
        // Product B: sell lower revenue, no returns
        var vB = await CreateVariantAsync(token, "Dash-NetOrdB");
        var reasonId = await CreateReasonCodeAsync(token, $"DO_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, vA, warehouseId, 200m);
        await SeedStock(token, vB, warehouseId, 200m);

        // A: gross 500 (10 * 50), return 400 (8 * 50) → net 100
        await CreateAndPostInvoice(token, warehouseId, vA, 10m, 50m);
        await CreateAndPostSalesReturn(token, warehouseId, vA, null, null, 8m, 50m, reasonId);

        // B: gross 200 (10 * 20), no returns → net 200
        await CreateAndPostInvoice(token, warehouseId, vB, 10m, 20m);

        var from = DateTime.UtcNow.AddHours(-1).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/top-products?from={from}&to={to}&orderBy=revenue&topN=100", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var products = await resp.Content.ReadFromJsonAsync<List<TopProductResp>>(JsonOpts);
        Assert.NotNull(products);

        var idxA = products!.FindIndex(p => p.VariantId == vA);
        var idxB = products.FindIndex(p => p.VariantId == vB);
        Assert.True(idxA >= 0 && idxB >= 0, "Both variants should be in results");

        // B (net 200) should appear before A (net 100) — sorted by net revenue desc
        Assert.True(idxB < idxA, $"B (net 200) should rank above A (net 100). idxB={idxB}, idxA={idxA}");
    }

    [Fact]
    public async Task SalesSummary_NoReturns_NetEqualsGross()
    {
        var token = await LoginAsAdminAsync();

        // Query with future range where no returns exist
        var futureFrom = DateTime.UtcNow.AddDays(10).ToString("O");
        var futureTo = DateTime.UtcNow.AddDays(11).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/sales?from={futureFrom}&to={futureTo}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var sales = await resp.Content.ReadFromJsonAsync<SalesSummaryResp>(JsonOpts);
        Assert.NotNull(sales);

        Assert.Equal(0, sales!.InvoiceCount);
        Assert.Equal(0, sales.ReturnCount);
        Assert.Equal(sales.TotalSales, sales.NetSales);
        Assert.Equal(0m, sales.TotalSalesReturns);
        Assert.Equal(0m, sales.NetAverageTicket);
    }

    [Fact]
    public async Task Summary_IncludesDispositionLoss()
    {
        var token = await LoginAsAdminAsync();

        var from = DateTime.UtcNow.AddDays(-30).ToString("O");
        var to = DateTime.UtcNow.AddHours(1).ToString("O");

        var resp = await GetAuth($"/api/v1/dashboard/summary?from={from}&to={to}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var summary = await resp.Content.ReadFromJsonAsync<DashboardSummaryResp>(JsonOpts);
        Assert.NotNull(summary);

        // DispositionLoss is a decimal >= 0 — just verify the field exists and is valid
        Assert.True(summary!.DispositionLoss >= 0m);
    }

    // ───── Helpers ─────

    private async Task<Guid> CreateAndPostInvoice(
        string token, Guid warehouseId, Guid variantId, decimal qty, decimal unitPrice)
    {
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = qty, unitPrice, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        postResp.EnsureSuccessStatusCode();

        return invoice.Id;
    }

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
            new { name = $"DashLimited-{Guid.NewGuid():N}", description = "Limited" });
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.Content.ReadFromJsonAsync<RoleResp>(JsonOpts);

        await PutAuth($"/api/v1/roles/{role!.Id}/permissions", adminToken,
            new { permissionCodes = permissions });

        var username = $"dashlim-{Guid.NewGuid():N}";
        await PostAuth("/api/v1/users", adminToken,
            new { username, password = "Limited@123!", roleIds = new[] { role.Id } });

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "Limited@123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);

        return (loginBody!.AccessToken, role.Id);
    }

    private async Task<Guid> CreateVariantAsync(string token, string name)
    {
        var prodResp = await PostAuth("/api/v1/products", token, new { name });
        prodResp.EnsureSuccessStatusCode();
        var product = await prodResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);

        var varResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"DASH-{Guid.NewGuid():N}",
            barcode = $"DASH-BC-{Guid.NewGuid():N}",
            color = "Black",
            size = "L",
            retailPrice = 100m,
            wholesalePrice = 80m
        });
        varResp.EnsureSuccessStatusCode();
        var variant = await varResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return variant!.Id;
    }

    private async Task<Guid> GetDefaultWarehouseIdAsync(string token)
    {
        var resp = await GetAuth("/api/v1/warehouses", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedResultResp<WarehouseItemResp>>(JsonOpts);
        return body!.Items.First(w => w.IsDefault).Id;
    }

    private async Task SeedStock(string token, Guid variantId, Guid warehouseId, decimal qty)
    {
        var resp = await PostAuth("/api/v1/stock-movements/post", token, new
        {
            type = 0, // OpeningBalance
            reference = $"DASH-SEED-{Guid.NewGuid():N}",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = qty, unitCost = (decimal?)1m, reason = (string?)null }
            }
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateReasonCodeAsync(string token, string code)
    {
        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr = $"سبب-{code}",
            description = $"Reason-{code}",
            category = "SalesReturn",
            requiresManagerApproval = false
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ReasonCodeResp>(JsonOpts);
        return body!.Id;
    }

    /// <summary>
    /// Creates a sales return and posts it. Returns the return ID.
    /// Uses dispositionType = 3 (ReturnToStock) by default.
    /// </summary>
    private async Task<Guid> CreateAndPostSalesReturn(
        string token, Guid warehouseId, Guid variantId,
        Guid? customerId, Guid? originalInvoiceId,
        decimal qty, decimal unitPrice, Guid reasonCodeId)
    {
        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId,
            originalSalesInvoiceId = originalInvoiceId,
            lines = new[]
            {
                new { variantId, quantity = qty, unitPrice, reasonCodeId, dispositionType = 3 /* ReturnToStock */, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnIdResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/sales-returns/{returnDto!.Id}/post", token, new { });
        postResp.EnsureSuccessStatusCode();

        return returnDto.Id;
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

    private async Task<HttpResponseMessage> PutAuth(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    // ── Response DTOs ──

    private sealed record LoginResp(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    private sealed record IdResp(Guid Id);
    private sealed record RoleResp(Guid Id, string Name);
    private sealed record InvoiceResp(Guid Id, string InvoiceNumber, decimal TotalAmount, string Status);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
    private sealed record PagedResultResp<T>(List<T> Items, int TotalCount, int Page, int PageSize);
    private sealed record ReasonCodeResp(Guid Id, string Code);
    private sealed record ReturnIdResp(Guid Id);

    private sealed record SalesSummaryResp(
        decimal TotalSales, int InvoiceCount, decimal AverageTicket,
        decimal TotalSalesReturns, int ReturnCount, decimal NetSales, decimal NetAverageTicket);

    private sealed record TopProductResp(
        Guid ProductId, string ProductName,
        Guid VariantId, string Sku,
        string? Color, string? Size,
        decimal TotalQuantity, decimal TotalRevenue,
        decimal ReturnedQuantity, decimal ReturnedRevenue,
        decimal NetQuantity, decimal NetRevenue);

    private sealed record LowStockAlertResp(
        Guid VariantId, string Sku, string ProductName,
        string? Color, string? Size,
        Guid WarehouseId, string WarehouseCode, string WarehouseName,
        decimal CurrentStock, decimal Threshold);

    private sealed record CashierPerfResp(
        Guid CashierUserId, string CashierUsername,
        int InvoiceCount, decimal TotalSales, decimal AverageTicket);

    private sealed record DashboardSummaryResp(
        SalesSummaryResp Sales,
        List<TopProductResp> TopProductsByQuantity,
        List<TopProductResp> TopProductsByRevenue,
        List<LowStockAlertResp> LowStockAlerts,
        List<CashierPerfResp> CashierPerformance,
        decimal DispositionLoss);
}

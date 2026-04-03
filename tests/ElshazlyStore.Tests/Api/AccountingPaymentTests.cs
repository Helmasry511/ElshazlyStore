using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for Phase 7: AR/AP Accounting, Payments, and Opening Balance/Payment Imports.
/// Covers:
/// - Balances computed correctly from ledger entries
/// - WalletName required for EWallet
/// - Import requires correct permissions
/// - Payments reduce outstanding
/// - Overpayment disallowed by default
/// </summary>
[Collection("Integration")]
public sealed class AccountingPaymentTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AccountingPaymentTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ───── Balance Computation Tests ─────

    [Fact]
    public async Task PostSalesInvoice_CreatesLedgerEntry_BalanceComputed()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-Balance");
        var customerId = await CreateCustomerAsync(token, "BalanceCust");

        // Seed stock
        await SeedStock(token, variantId, warehouseId, 100m);

        // Create and post a sales invoice for 250 (10 * 25)
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 10m, unitPrice = 25m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Check customer balance via accounting endpoint
        var balanceResp = await GetAuth($"/api/v1/accounting/balances/customer/{customerId}", token);
        Assert.Equal(HttpStatusCode.OK, balanceResp.StatusCode);
        var balance = await balanceResp.Content.ReadFromJsonAsync<BalanceResp>(JsonOpts);
        Assert.Equal(250m, balance!.Outstanding);
    }

    [Fact]
    public async Task PostPurchaseReceipt_CreatesLedgerEntry_BalanceComputed()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-PurchBal");
        var supplierId = await CreateSupplierAsync(token, "BalanceSupplier");

        // Create and post a purchase receipt for 500 (10 * 50)
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[] { new { variantId, quantity = 10m, unitCost = 50m } }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Check supplier balance via accounting endpoint
        var balanceResp = await GetAuth($"/api/v1/accounting/balances/supplier/{supplierId}", token);
        Assert.Equal(HttpStatusCode.OK, balanceResp.StatusCode);
        var balance = await balanceResp.Content.ReadFromJsonAsync<BalanceResp>(JsonOpts);
        Assert.Equal(500m, balance!.Outstanding);
    }

    [Fact]
    public async Task Payment_ReducesOutstandingBalance()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-PayReduce");
        var customerId = await CreateCustomerAsync(token, "PayReduceCust");

        // Seed stock, create and post invoice for 300
        await SeedStock(token, variantId, warehouseId, 100m);
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 10m, unitPrice = 30m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });

        // Make payment of 100
        var payResp = await PostAuth("/api/v1/payments", token, new
        {
            partyType = "customer",
            partyId = customerId,
            amount = 100m,
            method = "Cash"
        });
        Assert.Equal(HttpStatusCode.Created, payResp.StatusCode);

        // Balance should now be 200
        var balanceResp = await GetAuth($"/api/v1/accounting/balances/customer/{customerId}", token);
        var balance = await balanceResp.Content.ReadFromJsonAsync<BalanceResp>(JsonOpts);
        Assert.Equal(200m, balance!.Outstanding);
    }

    // ───── Payment Method Tests ─────

    [Fact]
    public async Task Payment_EWallet_RequiresWalletName()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-EWallet");
        var customerId = await CreateCustomerAsync(token, "EWalletCust");

        // Create balance
        await SeedStock(token, variantId, warehouseId, 100m);
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 5m, unitPrice = 20m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });

        // Try EWallet payment without walletName — should fail
        var payResp = await PostAuth("/api/v1/payments", token, new
        {
            partyType = "customer",
            partyId = customerId,
            amount = 50m,
            method = "EWallet"
            // walletName omitted
        });
        Assert.Equal(HttpStatusCode.BadRequest, payResp.StatusCode);
        var body = await payResp.Content.ReadAsStringAsync();
        Assert.Contains("WALLET_NAME_REQUIRED", body);
    }

    [Fact]
    public async Task Payment_EWallet_WithWalletName_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-EWalletOk");
        var customerId = await CreateCustomerAsync(token, "EWalletOkCust");

        await SeedStock(token, variantId, warehouseId, 100m);
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 5m, unitPrice = 20m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });

        // EWallet with walletName — should succeed
        var payResp = await PostAuth("/api/v1/payments", token, new
        {
            partyType = "customer",
            partyId = customerId,
            amount = 50m,
            method = "EWallet",
            walletName = "Vodafone Cash"
        });
        Assert.Equal(HttpStatusCode.Created, payResp.StatusCode);

        var payment = await payResp.Content.ReadFromJsonAsync<PaymentResp>(JsonOpts);
        Assert.Equal("EWallet", payment!.Method);
        Assert.Equal("Vodafone Cash", payment.WalletName);
    }

    [Fact]
    public async Task Payment_AllMethods_Accepted()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-AllMethods");
        var customerId = await CreateCustomerAsync(token, "AllMethodCust");

        // Create balance of 400
        await SeedStock(token, variantId, warehouseId, 100m);
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 10m, unitPrice = 40m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });

        // Pay 100 each with Cash, InstaPay, Visa, EWallet
        var methods = new (string method, string? walletName)[]
        {
            ("Cash", null), ("InstaPay", null), ("Visa", null), ("EWallet", "MyWallet")
        };

        foreach (var (method, walletName) in methods)
        {
            var payResp = await PostAuth("/api/v1/payments", token, new
            {
                partyType = "customer",
                partyId = customerId,
                amount = 100m,
                method,
                walletName
            });
            Assert.Equal(HttpStatusCode.Created, payResp.StatusCode);
        }

        // Balance should now be 0
        var balanceResp = await GetAuth($"/api/v1/accounting/balances/customer/{customerId}", token);
        var balance = await balanceResp.Content.ReadFromJsonAsync<BalanceResp>(JsonOpts);
        Assert.Equal(0m, balance!.Outstanding);
    }

    // ───── Overpayment Tests ─────

    [Fact]
    public async Task Payment_OverpayDisallowed_ByDefault()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-Overpay");
        var customerId = await CreateCustomerAsync(token, "OverpayCust");

        // Create balance of 100
        await SeedStock(token, variantId, warehouseId, 100m);
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 5m, unitPrice = 20m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });

        // Try to pay 200 (more than outstanding 100) — should fail
        var payResp = await PostAuth("/api/v1/payments", token, new
        {
            partyType = "customer",
            partyId = customerId,
            amount = 200m,
            method = "Cash"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, payResp.StatusCode);
        var body = await payResp.Content.ReadAsStringAsync();
        Assert.Contains("OVERPAYMENT_NOT_ALLOWED", body);
    }

    // ───── Import Permission Tests ─────

    [Fact]
    public async Task ImportOpeningBalances_RequiresPermission()
    {
        var (limitedToken, _) = await CreateLimitedUserAsync("PRODUCTS_READ");

        var csv = "PartyType,PartyCode,Amount\nCustomer,CUST-000001,100\n";
        var resp = await UploadCsv("/api/v1/imports/opening-balances/preview", limitedToken, csv, "balances.csv");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ImportPayments_RequiresPermission()
    {
        var (limitedToken, _) = await CreateLimitedUserAsync("PRODUCTS_READ");

        var csv = "PartyType,PartyCode,Amount,Method\nCustomer,CUST-000001,100,Cash\n";
        var resp = await UploadCsv("/api/v1/imports/payments/preview", limitedToken, csv, "payments.csv");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ImportOpeningBalances_Preview_DetectsErrors()
    {
        var token = await LoginAsAdminAsync();

        // CSV with errors: missing partytype, invalid amount
        var csv = "PartyType,PartyCode,Amount\n"
                + ",CUST-000001,100\n"            // missing partytype
                + "Customer,NONEXIST,-50\n";       // nonexistent code + negative amount

        var resp = await UploadCsv("/api/v1/imports/opening-balances/preview", token, csv, "balances.csv");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = await resp.Content.ReadFromJsonAsync<PreviewResult>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal(2, result!.TotalRows);

        // Both rows should have errors
        Assert.True(result.RowErrors[0].Count > 0, "Row 1 should have errors (missing PartyType).");
        Assert.True(result.RowErrors[1].Count > 0, "Row 2 should have errors (invalid code/amount).");
    }

    [Fact]
    public async Task ImportOpeningBalances_Commit_CreatesLedgerEntries()
    {
        var token = await LoginAsAdminAsync();
        var customerId = await CreateCustomerAsync(token, "ImportBalCust");

        // Get the customer code
        var custResp = await GetAuth($"/api/v1/customers?q=ImportBalCust", token);
        var customers = await custResp.Content.ReadFromJsonAsync<PagedResultResp<CustomerResp>>(JsonOpts);
        var custCode = customers!.Items.First(c => c.Name == "ImportBalCust").Code;

        var csv = $"PartyType,PartyCode,Amount,Reference\n"
                + $"Customer,{custCode},500,Opening balance import\n";

        // Preview
        var previewResp = await UploadCsv("/api/v1/imports/opening-balances/preview", token, csv, "balances.csv");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var preview = await previewResp.Content.ReadFromJsonAsync<PreviewResult>(JsonOpts);
        Assert.Equal(1, preview!.ValidRows);

        // Commit
        var commitResp = await PostAuth("/api/v1/imports/opening-balances/commit", token,
            new { jobId = preview.JobId });
        Assert.Equal(HttpStatusCode.OK, commitResp.StatusCode);

        // Verify balance
        var balanceResp = await GetAuth($"/api/v1/accounting/balances/customer/{customerId}", token);
        var balance = await balanceResp.Content.ReadFromJsonAsync<BalanceResp>(JsonOpts);
        Assert.Equal(500m, balance!.Outstanding);
    }

    [Fact]
    public async Task ImportPayments_Preview_DetectsWalletNameRequired()
    {
        var token = await LoginAsAdminAsync();
        var customerId = await CreateCustomerAsync(token, "PayImportCust");

        var custResp = await GetAuth($"/api/v1/customers?q=PayImportCust", token);
        var customers = await custResp.Content.ReadFromJsonAsync<PagedResultResp<CustomerResp>>(JsonOpts);
        var custCode = customers!.Items.First(c => c.Name == "PayImportCust").Code;

        // EWallet without WalletName
        var csv = $"PartyType,PartyCode,Amount,Method,WalletName\n"
                + $"Customer,{custCode},100,EWallet,\n";  // missing wallet name

        var resp = await UploadCsv("/api/v1/imports/payments/preview", token, csv, "payments.csv");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = await resp.Content.ReadFromJsonAsync<PreviewResult>(JsonOpts);
        Assert.True(result!.RowErrors[0].Count > 0, "Should have WalletName error.");
    }

    // ───── Ledger Query Tests ─────

    [Fact]
    public async Task GetLedger_ReturnsEntries()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Acct-Ledger");
        var customerId = await CreateCustomerAsync(token, "LedgerCust");

        await SeedStock(token, variantId, warehouseId, 100m);
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 5m, unitPrice = 10m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });

        // Pay 20
        await PostAuth("/api/v1/payments", token, new
        {
            partyType = "customer",
            partyId = customerId,
            amount = 20m,
            method = "Cash"
        });

        // Get ledger
        var ledgerResp = await GetAuth($"/api/v1/accounting/ledger/customer/{customerId}", token);
        Assert.Equal(HttpStatusCode.OK, ledgerResp.StatusCode);
        var ledger = await ledgerResp.Content.ReadFromJsonAsync<PagedResultResp<LedgerEntryResp>>(JsonOpts);
        Assert.True(ledger!.TotalCount >= 2); // at least invoice entry + payment entry
    }

    // ───── Customer/Supplier Balances List ─────

    [Fact]
    public async Task GetCustomerBalances_ReturnsPagedResults()
    {
        var token = await LoginAsAdminAsync();
        var resp = await GetAuth("/api/v1/accounting/balances/customers", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetSupplierBalances_ReturnsPagedResults()
    {
        var token = await LoginAsAdminAsync();
        var resp = await GetAuth("/api/v1/accounting/balances/suppliers", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
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
            new { name = $"Limited-{Guid.NewGuid():N}", description = "Limited" });
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.Content.ReadFromJsonAsync<RoleResp>(JsonOpts);

        await PutAuth($"/api/v1/roles/{role!.Id}/permissions", adminToken,
            new { permissionCodes = permissions });

        var username = $"limited-{Guid.NewGuid():N}";
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
            sku = $"ACCT-{Guid.NewGuid():N}",
            barcode = $"ACCT-BC-{Guid.NewGuid():N}",
            color = "Black",
            size = "L",
            retailPrice = 100m,
            wholesalePrice = 80m
        });
        varResp.EnsureSuccessStatusCode();
        var variant = await varResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return variant!.Id;
    }

    private async Task<Guid> CreateCustomerAsync(string token, string name)
    {
        var resp = await PostAuth("/api/v1/customers", token, new { name, phone = "01000000000" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return body!.Id;
    }

    private async Task<Guid> CreateSupplierAsync(string token, string name)
    {
        var resp = await PostAuth("/api/v1/suppliers", token, new { name, phone = "01000000000" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return body!.Id;
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
            reference = $"ACCT-SEED-{Guid.NewGuid():N}",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = qty, unitCost = (decimal?)1m, reason = (string?)null }
            }
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> UploadCsv(string url, string token, string csvContent, string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
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
    private sealed record InvoiceResp(Guid Id, string InvoiceNumber, decimal TotalAmount, Guid? CustomerId, string Status, Guid? StockMovementId);
    private sealed record ReceiptResp(Guid Id, string DocumentNumber);
    private sealed record BalanceResp(Guid PartyId, string PartyType, decimal Outstanding);
    private sealed record LedgerEntryResp(Guid Id, string EntryType, decimal Amount, string? Reference);
    private sealed record PaymentResp(Guid Id, decimal Amount, string Method, string? WalletName);
    private sealed record CustomerResp(Guid Id, string Code, string Name);
    private sealed record PagedResultResp<T>(List<T> Items, int TotalCount, int Page, int PageSize);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
    private sealed record PreviewResult(Guid JobId, int TotalRows, int ValidRows, List<List<RowError>> RowErrors);
    private sealed record RowError(string Column, string Message);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for Phase RET 1: Sales Returns (Customer).
/// Covers:
/// - Create draft sales return (walk-in and with customer)
/// - Post creates correct stock movement (SaleReturnReceipt)
/// - Cannot return more than sold (linked invoice validation)
/// - Double post prevented (idempotent / concurrency-safe)
/// - Reason code must be active
/// - Disposition types control stock receipt
/// - Void posted return
/// - CRUD operations on draft
/// </summary>
[Collection("Integration")]
public sealed class SalesReturnTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesReturnTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ───── Create Tests ─────

    [Fact]
    public async Task CreateSalesReturn_WalkIn_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-WalkIn");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_WI_{Guid.NewGuid():N}"[..30]);

        var resp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId = (Guid?)null,
            notes = "Walk-in return",
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 50m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("Draft", body!.Status);
        Assert.Null(body.CustomerId);
        Assert.StartsWith("RET-", body.ReturnNumber);
        Assert.Single(body.Lines);
        Assert.Equal(50m, body.TotalAmount);
    }

    [Fact]
    public async Task CreateSalesReturn_WithCustomerAndInvoice_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-WithCust");
        var customerId = await CreateCustomerAsync(token, "RetCust");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_WC_{Guid.NewGuid():N}"[..30]);

        // Seed stock and create and post a sales invoice
        await SeedStock(token, variantId, warehouseId, 20m);
        var invoiceId = await CreateAndPostSalesInvoiceAsync(token, warehouseId, variantId, customerId, 10m, 50m);

        var resp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId,
            originalSalesInvoiceId = invoiceId,
            notes = "Return with invoice",
            lines = new[]
            {
                new { variantId, quantity = 2m, unitPrice = 50m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(customerId, body!.CustomerId);
        Assert.Equal(invoiceId, body.OriginalSalesInvoiceId);
        Assert.Equal(100m, body.TotalAmount); // 2 * 50
    }

    // ───── Post Tests ─────

    [Fact]
    public async Task PostSalesReturn_ReturnToStock_CreatesStockMovement()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Post-RTS");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_RTS_{Guid.NewGuid():N}"[..30]);

        // Seed initial stock
        await SeedStock(token, variantId, warehouseId, 100m);

        // Create and post a sales invoice (sells 5 units)
        var customerId = await CreateCustomerAsync(token, "Ret-Post-Cust");
        var invoiceId = await CreateAndPostSalesInvoiceAsync(token, warehouseId, variantId, customerId, 5m, 20m);

        // Check stock after sale
        var balanceAfterSale = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(95m, balanceAfterSale); // 100 - 5

        // Create a return for 3 units with ReturnToStock
        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId,
            originalSalesInvoiceId = invoiceId,
            lines = new[]
            {
                new { variantId, quantity = 3m, unitPrice = 20m, reasonCodeId = reasonId, dispositionType = 3 /* ReturnToStock */, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Post the return
        var postResp = await PostAuth($"/api/v1/sales-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var postBody = await postResp.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(postBody?.StockMovementId);

        // Check stock after return — should be 98 (95 + 3)
        var balanceAfterReturn = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(98m, balanceAfterReturn);
    }

    [Fact]
    public async Task CreateSalesReturn_ForbiddenDisposition_Returns400()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Disp-Bad");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_SC_{Guid.NewGuid():N}"[..30]);

        // Attempt to create a return with Scrap disposition (not allowed in RET 1)
        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 2m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 0 /* Scrap */, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, createResp.StatusCode);
        var problem = await createResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("SALES_RETURN_DISPOSITION_NOT_ALLOWED", problem!.Title);
    }

    // ───── Return Qty Validation ─────

    [Fact]
    public async Task PostSalesReturn_CannotReturnMoreThanSold()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Exceed");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_EX_{Guid.NewGuid():N}"[..30]);

        // Seed stock and sell 5
        await SeedStock(token, variantId, warehouseId, 100m);
        var customerId = await CreateCustomerAsync(token, "Ret-Exceed-Cust");
        var invoiceId = await CreateAndPostSalesInvoiceAsync(token, warehouseId, variantId, customerId, 5m, 10m);

        // Try to return 6 (more than sold 5)
        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId,
            originalSalesInvoiceId = invoiceId,
            lines = new[]
            {
                new { variantId, quantity = 6m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });

        // Should fail at creation time since validation happens there too
        Assert.Equal(HttpStatusCode.BadRequest, createResp.StatusCode);
        var problem = await createResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("RETURN_QTY_EXCEEDS_SOLD", problem!.Title);
    }

    [Fact]
    public async Task PostSalesReturn_CumulativeReturnQtyEnforced()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Cumul");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_CU_{Guid.NewGuid():N}"[..30]);

        // Seed and sell 10
        await SeedStock(token, variantId, warehouseId, 100m);
        var customerId = await CreateCustomerAsync(token, "Ret-Cumul-Cust");
        var invoiceId = await CreateAndPostSalesInvoiceAsync(token, warehouseId, variantId, customerId, 10m, 10m);

        // First return: 7 units (should succeed)
        var createResp1 = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId,
            originalSalesInvoiceId = invoiceId,
            lines = new[]
            {
                new { variantId, quantity = 7m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp1.EnsureSuccessStatusCode();
        var ret1 = await createResp1.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var postResp1 = await PostAuth($"/api/v1/sales-returns/{ret1!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp1.StatusCode);

        // Second return: 4 units (should fail — only 3 remaining: 10 - 7 = 3)
        var createResp2 = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId,
            originalSalesInvoiceId = invoiceId,
            lines = new[]
            {
                new { variantId, quantity = 4m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, createResp2.StatusCode);
        var problem = await createResp2.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("RETURN_QTY_EXCEEDS_SOLD", problem!.Title);
    }

    // ───── Double Post Prevention ─────

    [Fact]
    public async Task PostSalesReturn_DoublePost_ReturnsAlreadyPosted()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Double");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_DB_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // First post
        var postResp1 = await PostAuth($"/api/v1/sales-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp1.StatusCode);

        // Second post — idempotent: returns OK with the same stock movement ID
        var postResp2 = await PostAuth($"/api/v1/sales-returns/{returnDto.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp2.StatusCode);

        // Stock should only increase once: seed 50 + return 1 = 51
        var balance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(51m, balance);
    }

    // ───── Reason Code Validation ─────

    [Fact]
    public async Task PostSalesReturn_InactiveReasonCode_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-InactRC");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_IR_{Guid.NewGuid():N}"[..30]);

        // Deactivate the reason code via the disable endpoint
        var disableResp = await PostAuth($"/api/v1/reasons/{reasonId}/disable", token, new { });
        disableResp.EnsureSuccessStatusCode();

        // Try to create return with inactive reason
        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, createResp.StatusCode);
        var problem = await createResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("REASON_CODE_INACTIVE", problem!.Title);
    }

    // ───── Void Tests ─────

    [Fact]
    public async Task VoidSalesReturn_DraftReturn_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Void-D");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_VD_{Guid.NewGuid():N}"[..30]);

        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Void the draft
        var voidResp = await PostAuth($"/api/v1/sales-returns/{returnDto!.Id}/void", token, new { });
        Assert.Equal(HttpStatusCode.OK, voidResp.StatusCode);

        // Verify status is Voided
        var getResp = await GetAuth($"/api/v1/sales-returns/{returnDto.Id}", token);
        var body = await getResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.Equal("Voided", body!.Status);
    }

    [Fact]
    public async Task VoidSalesReturn_PostedReturn_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Void-P");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_VP_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Post the return
        var postResp = await PostAuth($"/api/v1/sales-returns/{returnDto!.Id}/post", token, new { });
        postResp.EnsureSuccessStatusCode();

        // Try to void a posted return — should be rejected
        var voidResp = await PostAuth($"/api/v1/sales-returns/{returnDto.Id}/void", token, new { });
        Assert.Equal(HttpStatusCode.Conflict, voidResp.StatusCode);
        var problem = await voidResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST", problem!.Title);
    }

    [Fact]
    public async Task PostSalesReturn_CreditNote_ReducesOutstanding()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Credit");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_CR_{Guid.NewGuid():N}"[..30]);
        var customerId = await CreateCustomerAsync(token, "Ret-Credit-Cust");

        // Seed stock and sell 10 units @ 50 each → Invoice = 500
        await SeedStock(token, variantId, warehouseId, 100m);
        var invoiceId = await CreateAndPostSalesInvoiceAsync(token, warehouseId, variantId, customerId, 10m, 50m);

        // Check outstanding after sale (should be 500)
        var balanceBefore = await GetCustomerOutstanding(token, customerId);
        Assert.Equal(500m, balanceBefore);

        // Return 3 units @ 50 → credit note = -150
        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            customerId,
            originalSalesInvoiceId = invoiceId,
            lines = new[]
            {
                new { variantId, quantity = 3m, unitPrice = 50m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/sales-returns/{returnDto!.Id}/post", token, new { });
        postResp.EnsureSuccessStatusCode();

        // Outstanding should now be 350 (500 - 150)
        var balanceAfter = await GetCustomerOutstanding(token, customerId);
        Assert.Equal(350m, balanceAfter);
    }

    // ───── CRUD Tests ─────

    [Fact]
    public async Task GetSalesReturn_ById()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-GetById");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_GB_{Guid.NewGuid():N}"[..30]);

        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 99m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var getResp = await GetAuth($"/api/v1/sales-returns/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var body = await getResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.Equal(created.ReturnNumber, body!.ReturnNumber);
    }

    [Fact]
    public async Task ListSalesReturns_SearchByNumber()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-List");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_LS_{Guid.NewGuid():N}"[..30]);

        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var listResp = await GetAuth($"/api/v1/sales-returns?q={created!.ReturnNumber}", token);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<PagedReturnResp>(JsonOpts);
        Assert.True(body!.TotalCount >= 1);
        Assert.Contains(body.Items, r => r.ReturnNumber == created.ReturnNumber);
    }

    [Fact]
    public async Task UpdateDraftReturn_ChangesLines()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v1 = await CreateVariantAsync(token, "Ret-Upd-V1");
        var v2 = await CreateVariantAsync(token, "Ret-Upd-V2");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_UP_{Guid.NewGuid():N}"[..30]);

        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId = v1, quantity = 1m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Update lines
        var updateResp = await PutAuth($"/api/v1/sales-returns/{created!.Id}", token, new
        {
            notes = "Updated",
            lines = new[]
            {
                new { variantId = v2, quantity = 5m, unitPrice = 20m, reasonCodeId = reasonId, dispositionType = 4 /* Quarantine */, notes = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var body = await updateResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.Equal("Updated", body!.Notes);
        Assert.Single(body.Lines);
        Assert.Equal(v2, body.Lines[0].VariantId);
        Assert.Equal(100m, body.TotalAmount); // 5 * 20
    }

    [Fact]
    public async Task DeleteDraftReturn_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Ret-Del");
        var reasonId = await CreateReasonCodeAsync(token, $"RET_DL_{Guid.NewGuid():N}"[..30]);

        var createResp = await PostAuth("/api/v1/sales-returns", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, reasonCodeId = reasonId, dispositionType = 3, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var deleteResp = await DeleteAuth($"/api/v1/sales-returns/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        // Verify deleted
        var getResp = await GetAuth($"/api/v1/sales-returns/{created.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task SalesReturns_RequiresAuthentication()
    {
        var resp = await _client.GetAsync("/api/v1/sales-returns");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ───── Helpers ─────

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "Admin@123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResp>(JsonOpts);
        return body!.AccessToken;
    }

    private async Task<Guid> CreateVariantAsync(string token, string name)
    {
        var prodResp = await PostAuth("/api/v1/products", token, new { name });
        prodResp.EnsureSuccessStatusCode();
        var product = await prodResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);

        var varResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"RET-{Guid.NewGuid():N}",
            barcode = $"RET-BC-{Guid.NewGuid():N}",
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
        var resp = await PostAuth("/api/v1/customers", token,
            new { name = $"{name}-{Guid.NewGuid():N}", phone = "01000000000" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return body!.Id;
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
            category = "SalesReturn"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ReasonResp>(JsonOpts);
        return body!.Id;
    }

    private async Task SeedStock(string token, Guid variantId, Guid warehouseId, decimal qty)
    {
        var resp = await PostAuth("/api/v1/stock-movements/post", token, new
        {
            type = 0, // OpeningBalance
            reference = $"RET-SEED-{Guid.NewGuid():N}",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = qty, unitCost = (decimal?)1m, reason = (string?)null }
            }
        });
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Creates a sales invoice and posts it.
    /// Assumes stock already exists for the sale.
    /// Returns the invoice ID.
    /// </summary>
    private async Task<Guid> CreateAndPostSalesInvoiceAsync(
        string token, Guid warehouseId, Guid variantId, Guid? customerId,
        decimal qty, decimal unitPrice)
    {
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[]
            {
                new { variantId, quantity = qty, unitPrice, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        postResp.EnsureSuccessStatusCode();

        return invoice.Id;
    }

    private async Task<decimal> GetBalanceForVariant(string token, Guid warehouseId, Guid variantId)
    {
        var resp = await GetAuth($"/api/v1/stock/balances?warehouseId={warehouseId}&pageSize=200", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedBalanceResp>(JsonOpts);
        var item = body!.Items.FirstOrDefault(b => b.VariantId == variantId);
        return item?.Quantity ?? 0m;
    }

    private async Task<decimal> GetCustomerOutstanding(string token, Guid customerId)
    {
        var resp = await GetAuth($"/api/v1/accounting/balances/customers?pageSize=200", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedPartyBalanceResp>(JsonOpts);
        var item = body!.Items.FirstOrDefault(b => b.PartyId == customerId);
        return item?.Outstanding ?? 0m;
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
    private sealed record ReasonResp(Guid Id, string Code);
    private sealed record ReturnResp(
        Guid Id, string ReturnNumber, DateTime ReturnDateUtc,
        Guid? CustomerId, string? CustomerName,
        Guid? OriginalSalesInvoiceId, string? OriginalInvoiceNumber,
        Guid WarehouseId, string WarehouseName,
        Guid CreatedByUserId, string CreatedByUsername,
        string? Notes, string Status,
        Guid? StockMovementId, decimal TotalAmount,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        Guid? PostedByUserId,
        List<ReturnLineResp> Lines);
    private sealed record ReturnLineResp(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal UnitPrice, decimal LineTotal,
        Guid ReasonCodeId, string ReasonCodeCode, string ReasonCodeNameAr,
        string DispositionType, string? Notes);
    private sealed record PostResultResp(Guid? StockMovementId);
    private sealed record InvoiceResp(Guid Id, string InvoiceNumber);
    private sealed record ProblemResp(string Title, string Detail);
    private sealed record PagedReturnResp(List<ReturnResp> Items, int TotalCount);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
    private sealed record PagedPartyBalanceResp(List<PartyBalanceItemResp> Items, int TotalCount);
    private sealed record PartyBalanceItemResp(Guid PartyId, string PartyName, decimal Outstanding);
}

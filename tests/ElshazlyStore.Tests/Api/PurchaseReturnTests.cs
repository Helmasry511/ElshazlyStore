using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for Phase RET 2: Purchase Returns (To Supplier).
/// Covers:
/// - Create draft purchase return
/// - Create with original purchase receipt link
/// - Post creates correct stock movement (PurchaseReturnIssue, negative delta)
/// - Cannot return more than received (linked receipt validation)
/// - Cumulative return qty enforcement
/// - Double post prevented (idempotent / concurrency-safe)
/// - Reason code must be active
/// - Void draft return
/// - Cannot void posted return
/// - DebitNote ledger entry reduces supplier payable
/// - Get by ID
/// - List/search
/// - Update draft lines
/// - Delete draft
/// - Requires authentication
/// </summary>
[Collection("Integration")]
public sealed class PurchaseReturnTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PurchaseReturnTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ───── Create Tests ─────

    [Fact]
    public async Task CreatePurchaseReturn_Basic_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Basic");
        var variantId = await CreateVariantAsync(token, "PRet-Basic");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_B_{Guid.NewGuid():N}"[..30]);

        // Seed stock so we have inventory to return
        await SeedStock(token, variantId, warehouseId, 50m);

        var resp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId,
            warehouseId,
            notes = "Basic purchase return",
            lines = new[]
            {
                new { variantId, quantity = 5m, unitCost = 10m, reasonCodeId = reasonId, dispositionType = 2 /* ReturnToVendor */, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("Draft", body!.Status);
        Assert.Equal(supplierId, body.SupplierId);
        Assert.StartsWith("PRET-", body.ReturnNumber);
        Assert.Single(body.Lines);
        Assert.Equal(50m, body.TotalAmount); // 5 * 10
    }

    [Fact]
    public async Task CreatePurchaseReturn_WithOriginalReceipt_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-WithReceipt");
        var variantId = await CreateVariantAsync(token, "PRet-WithReceipt");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_WR_{Guid.NewGuid():N}"[..30]);

        // Create and post a purchase receipt (receives 20 units)
        var receiptId = await CreateAndPostPurchaseReceiptAsync(token, supplierId, warehouseId, variantId, 20m, 15m);

        var resp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId,
            warehouseId,
            originalPurchaseReceiptId = receiptId,
            notes = "Return with receipt",
            lines = new[]
            {
                new { variantId, quantity = 5m, unitCost = 15m, reasonCodeId = reasonId, dispositionType = 2 /* ReturnToVendor */, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(receiptId, body!.OriginalPurchaseReceiptId);
        Assert.Equal(75m, body.TotalAmount); // 5 * 15
    }

    // ───── Post Tests ─────

    [Fact]
    public async Task PostPurchaseReturn_RemovesStockAndCreatesMovement()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Post");
        var variantId = await CreateVariantAsync(token, "PRet-Post");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_PO_{Guid.NewGuid():N}"[..30]);

        // Seed stock
        await SeedStock(token, variantId, warehouseId, 100m);

        // Create a purchase return for 10 units
        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId,
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 10m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Post the return
        var postResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var postBody = await postResp.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(postBody?.StockMovementId);

        // Check stock after return — should be 90 (100 - 10)
        var balanceAfterReturn = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(90m, balanceAfterReturn);
    }

    [Fact]
    public async Task PostPurchaseReturn_NegativeStockPrevented()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-NegStock");
        var variantId = await CreateVariantAsync(token, "PRet-NegStock");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_NS_{Guid.NewGuid():N}"[..30]);

        // Seed only 5 units of stock
        await SeedStock(token, variantId, warehouseId, 5m);

        // Try to return 10 (more than available)
        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId,
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 10m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Post should fail due to negative stock
        var postResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal((HttpStatusCode)422, postResp.StatusCode);
        var problem = await postResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("STOCK_NEGATIVE_NOT_ALLOWED", problem!.Title);
    }

    // ───── Return Qty Validation ─────

    [Fact]
    public async Task CreatePurchaseReturn_CannotReturnMoreThanReceived()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Exceed");
        var variantId = await CreateVariantAsync(token, "PRet-Exceed");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_EX_{Guid.NewGuid():N}"[..30]);

        // Receive 10 units
        var receiptId = await CreateAndPostPurchaseReceiptAsync(token, supplierId, warehouseId, variantId, 10m, 5m);

        // Try to return 15 (more than received 10)
        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId,
            warehouseId,
            originalPurchaseReceiptId = receiptId,
            lines = new[]
            {
                new { variantId, quantity = 15m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, createResp.StatusCode);
        var problem = await createResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("RETURN_QTY_EXCEEDS_RECEIVED", problem!.Title);
    }

    [Fact]
    public async Task PostPurchaseReturn_CumulativeReturnQtyEnforced()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Cumul");
        var variantId = await CreateVariantAsync(token, "PRet-Cumul");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_CU_{Guid.NewGuid():N}"[..30]);

        // Receive 10 units
        var receiptId = await CreateAndPostPurchaseReceiptAsync(token, supplierId, warehouseId, variantId, 10m, 5m);

        // Return 6 and post (succeeds)
        var ret1 = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId, originalPurchaseReceiptId = receiptId,
            lines = new[] { new { variantId, quantity = 6m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        ret1.EnsureSuccessStatusCode();
        var ret1Dto = await ret1.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        var post1 = await PostAuth($"/api/v1/purchase-returns/{ret1Dto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post1.StatusCode);

        // Try to return another 6 (total 12 > received 10)
        var ret2 = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId, originalPurchaseReceiptId = receiptId,
            lines = new[] { new { variantId, quantity = 6m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, ret2.StatusCode);
        var problem = await ret2.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("RETURN_QTY_EXCEEDS_RECEIVED", problem!.Title);
    }

    // ───── Double Post / Idempotency ─────

    [Fact]
    public async Task PostPurchaseReturn_DoublePostIsIdempotent()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Idemp");
        var variantId = await CreateVariantAsync(token, "PRet-Idemp");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_ID_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 3m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // First post
        var post1 = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post1.StatusCode);
        var body1 = await post1.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        // Second post — should succeed idempotently
        var post2 = await PostAuth($"/api/v1/purchase-returns/{returnDto.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post2.StatusCode);
        var body2 = await post2.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        Assert.Equal(body1!.StockMovementId, body2!.StockMovementId);

        // Verify stock only decreased once: 50 - 3 = 47
        var balance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(47m, balance);
    }

    // ───── Reason Code Validation ─────

    [Fact]
    public async Task PostPurchaseReturn_InactiveReasonCode_Returns400()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-InactiveRC");
        var variantId = await CreateVariantAsync(token, "PRet-InactiveRC");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_IR_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        // Create return with active reason code
        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 1m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Deactivate the reason code
        await PostAuth($"/api/v1/reasons/{reasonId}/disable", token, new { });

        // Post should fail
        var postResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.BadRequest, postResp.StatusCode);
        var problem = await postResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("REASON_CODE_INACTIVE", problem!.Title);
    }

    // ───── Void Tests ─────

    [Fact]
    public async Task VoidDraftPurchaseReturn_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Void");
        var variantId = await CreateVariantAsync(token, "PRet-Void");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_VD_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 2m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var voidResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/void", token, new { });
        Assert.Equal(HttpStatusCode.OK, voidResp.StatusCode);

        // Verify status is Voided
        var getResp = await GetAuth($"/api/v1/purchase-returns/{returnDto.Id}", token);
        var body = await getResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.Equal("Voided", body!.Status);
    }

    [Fact]
    public async Task VoidPostedPurchaseReturn_Returns409()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-VoidPosted");
        var variantId = await CreateVariantAsync(token, "PRet-VoidPosted");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_VP_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 1m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Post it first
        var postResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Try to void — should fail
        var voidResp = await PostAuth($"/api/v1/purchase-returns/{returnDto.Id}/void", token, new { });
        Assert.Equal(HttpStatusCode.Conflict, voidResp.StatusCode);
        var problem = await voidResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("PURCHASE_RETURN_VOID_NOT_ALLOWED_AFTER_POST", problem!.Title);
    }

    // ───── AP / Ledger ─────

    [Fact]
    public async Task PostPurchaseReturn_CreatesDebitNoteLedgerEntry()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Ledger");
        var variantId = await CreateVariantAsync(token, "PRet-Ledger");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_LG_{Guid.NewGuid():N}"[..30]);

        // Receive goods (creates supplier payable of 200 = 20 * 10)
        await CreateAndPostPurchaseReceiptAsync(token, supplierId, warehouseId, variantId, 20m, 10m);

        // Check supplier outstanding before return
        var outstandingBefore = await GetSupplierOutstanding(token, supplierId);
        Assert.Equal(200m, outstandingBefore); // 20 * 10

        // Return 5 units at cost 10 each (debit note of 50)
        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 5m, unitCost = 10m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var returnDto = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/purchase-returns/{returnDto!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Supplier outstanding should be reduced by 50: 200 - 50 = 150
        var outstandingAfter = await GetSupplierOutstanding(token, supplierId);
        Assert.Equal(150m, outstandingAfter);
    }

    // ───── CRUD ─────

    [Fact]
    public async Task GetPurchaseReturn_ById_ReturnsCorrectData()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Get");
        var variantId = await CreateVariantAsync(token, "PRet-Get");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_GT_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId, notes = "Get test",
            lines = new[] { new { variantId, quantity = 3m, unitCost = 7m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var getResp = await GetAuth($"/api/v1/purchase-returns/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var body = await getResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("Get test", body.Notes);
        Assert.Equal(21m, body.TotalAmount); // 3 * 7
    }

    [Fact]
    public async Task ListPurchaseReturns_ReturnsPaged()
    {
        var token = await LoginAsAdminAsync();
        var resp = await GetAuth("/api/v1/purchase-returns?pageSize=5", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<PagedReturnResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 0);
    }

    [Fact]
    public async Task UpdateDraftPurchaseReturn_UpdatesLines()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Update");
        var variantId = await CreateVariantAsync(token, "PRet-Update");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_UP_{Guid.NewGuid():N}"[..30]);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 1m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        // Update lines
        var updateResp = await PutAuth($"/api/v1/purchase-returns/{created!.Id}", token, new
        {
            notes = "Updated notes",
            lines = new[]
            {
                new { variantId, quantity = 4m, unitCost = 8m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);
        Assert.Equal(32m, updated!.TotalAmount); // 4 * 8
        Assert.Equal("Updated notes", updated.Notes);
    }

    [Fact]
    public async Task DeleteDraftPurchaseReturn_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var supplierId = await CreateSupplierAsync(token, "PRet-Del");
        var variantId = await CreateVariantAsync(token, "PRet-Del");
        var reasonId = await CreateReasonCodeAsync(token, $"PRET_DL_{Guid.NewGuid():N}"[..30]);

        var createResp = await PostAuth("/api/v1/purchase-returns", token, new
        {
            supplierId, warehouseId,
            lines = new[] { new { variantId, quantity = 1m, unitCost = 5m, reasonCodeId = reasonId, dispositionType = 2, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ReturnResp>(JsonOpts);

        var deleteResp = await DeleteAuth($"/api/v1/purchase-returns/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        // Verify deleted
        var getResp = await GetAuth($"/api/v1/purchase-returns/{created.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task PurchaseReturns_RequiresAuthentication()
    {
        var resp = await _client.GetAsync("/api/v1/purchase-returns");
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
            sku = $"PRET-{Guid.NewGuid():N}",
            barcode = $"PRET-BC-{Guid.NewGuid():N}",
            color = "Black",
            size = "L",
            retailPrice = 100m,
            wholesalePrice = 80m
        });
        varResp.EnsureSuccessStatusCode();
        var variant = await varResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);
        return variant!.Id;
    }

    private async Task<Guid> CreateSupplierAsync(string token, string name)
    {
        var resp = await PostAuth("/api/v1/suppliers", token,
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
            nameAr = "سبب مرتجع مشتريات",
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
            type = 0, // OpeningBalance
            reference = $"PRET-SEED-{Guid.NewGuid():N}",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = qty, unitCost = (decimal?)1m, reason = (string?)null }
            }
        });
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Creates a purchase receipt and posts it.
    /// Returns the receipt ID.
    /// </summary>
    private async Task<Guid> CreateAndPostPurchaseReceiptAsync(
        string token, Guid supplierId, Guid warehouseId, Guid variantId,
        decimal qty, decimal unitCost)
    {
        var createResp = await PostAuth("/api/v1/purchases", token, new
        {
            supplierId,
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = qty, unitCost }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var receipt = await createResp.Content.ReadFromJsonAsync<ReceiptResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/purchases/{receipt!.Id}/post", token, new { });
        postResp.EnsureSuccessStatusCode();

        return receipt.Id;
    }

    private async Task<decimal> GetBalanceForVariant(string token, Guid warehouseId, Guid variantId)
    {
        var resp = await GetAuth($"/api/v1/stock/balances?warehouseId={warehouseId}&pageSize=200", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedBalanceResp>(JsonOpts);
        var item = body!.Items.FirstOrDefault(b => b.VariantId == variantId);
        return item?.Quantity ?? 0m;
    }

    private async Task<decimal> GetSupplierOutstanding(string token, Guid supplierId)
    {
        var resp = await GetAuth($"/api/v1/accounting/balances/suppliers?pageSize=200", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedPartyBalanceResp>(JsonOpts);
        var item = body!.Items.FirstOrDefault(b => b.PartyId == supplierId);
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
    private sealed record ReceiptResp(Guid Id, string DocumentNumber, string Status, List<ReceiptLineResp> Lines);
    private sealed record ReceiptLineResp(Guid VariantId, decimal Quantity, decimal UnitCost);
    private sealed record ReturnResp(
        Guid Id, string ReturnNumber, DateTime ReturnDateUtc,
        Guid SupplierId, string SupplierName,
        Guid? OriginalPurchaseReceiptId, string? OriginalDocumentNumber,
        Guid WarehouseId, string WarehouseName,
        Guid CreatedByUserId, string CreatedByUsername,
        string? Notes, string Status,
        Guid? StockMovementId, decimal TotalAmount,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        Guid? PostedByUserId,
        List<ReturnLineResp> Lines);
    private sealed record ReturnLineResp(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal UnitCost, decimal LineTotal,
        Guid ReasonCodeId, string ReasonCodeCode, string ReasonCodeNameAr,
        string DispositionType, string? Notes);
    private sealed record PostResultResp(Guid? StockMovementId);
    private sealed record ProblemResp(string Title, string Detail);
    private sealed record PagedReturnResp(List<ReturnResp> Items, int TotalCount);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
    private sealed record PagedPartyBalanceResp(List<PartyBalanceItemResp> Items, int TotalCount);
    private sealed record PartyBalanceItemResp(Guid PartyId, string PartyName, decimal Outstanding);
}

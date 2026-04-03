using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for POS sales invoices: CRUD, posting reduces stock,
/// immutable invoice number, walk-in (no customer), barcode lookup, and search.
/// </summary>
[Collection("Integration")]
public sealed class SalesInvoiceTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesInvoiceTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateSalesInvoice_WalkIn_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-WalkIn");

        var resp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId = (Guid?)null,
            notes = "Walk-in sale",
            lines = new[]
            {
                new { variantId, quantity = 2m, unitPrice = 50m, discountAmount = (decimal?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("Draft", body!.Status);
        Assert.Null(body.CustomerId);
        Assert.StartsWith("INV-", body.InvoiceNumber);
        Assert.Single(body.Lines);
        Assert.Equal(100m, body.TotalAmount); // 2 * 50
    }

    [Fact]
    public async Task CreateSalesInvoice_WithCustomer_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-WithCust");
        var customerId = await CreateCustomerAsync(token, "SaleTestCust");

        var resp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[]
            {
                new { variantId, quantity = 3m, unitPrice = 20m, discountAmount = (decimal?)5m }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(customerId, body!.CustomerId);
        Assert.Equal(55m, body.TotalAmount); // (3 * 20) - 5 = 55
    }

    [Fact]
    public async Task CreateSalesInvoice_LocalInvoiceDate_IsNormalizedToUtc()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-LocalDate");
        var localInvoiceDate = DateTime.SpecifyKind(new DateTime(2026, 3, 28, 18, 30, 0), DateTimeKind.Local);

        var resp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            invoiceDateUtc = localInvoiceDate,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, discountAmount = (decimal?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(DateTimeKind.Utc, body!.InvoiceDateUtc.Kind);
        Assert.Equal(localInvoiceDate.ToUniversalTime(), body.InvoiceDateUtc);
    }

    [Fact]
    public async Task CreateSalesInvoice_UnspecifiedInvoiceDate_PreservesCalendarDateAsUtc()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-UnspecifiedDate");
        var unspecifiedInvoiceDate = DateTime.SpecifyKind(new DateTime(2026, 3, 28, 0, 0, 0), DateTimeKind.Unspecified);

        var resp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            invoiceDateUtc = unspecifiedInvoiceDate,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, discountAmount = (decimal?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(DateTimeKind.Utc, body!.InvoiceDateUtc.Kind);
        Assert.Equal(new DateTime(2026, 3, 28, 0, 0, 0, DateTimeKind.Utc), body.InvoiceDateUtc);
    }

    [Fact]
    public async Task GetSalesInvoice_ById()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-GetById");

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 1m, unitPrice = 99m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        var getResp = await GetAuth($"/api/v1/sales/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var body = await getResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.Equal(created.InvoiceNumber, body!.InvoiceNumber);
    }

    [Fact]
    public async Task GetSalesInvoice_ById_IncludesLinkedPaymentTrace()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-Trace");
        var customerId = await CreateCustomerAsync(token, "SaleTraceCustomer");

        await SeedStock(token, variantId, warehouseId, 100m);

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[] { new { variantId, quantity = 4m, unitPrice = 50m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/sales/{created!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var paymentResp = await PostAuth("/api/v1/payments", token, new
        {
            partyType = "customer",
            partyId = customerId,
            amount = 80m,
            method = "Cash",
            reference = "TRACE-80",
            relatedInvoiceId = created.Id
        });
        Assert.Equal(HttpStatusCode.Created, paymentResp.StatusCode);

        var getResp = await GetAuth($"/api/v1/sales/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var body = await getResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.NotNull(body!.PaymentTrace);
        Assert.Equal(80m, body.PaymentTrace!.PaidAmount);
        Assert.Equal(120m, body.PaymentTrace.RemainingAmount);
        Assert.Equal("Cash", body.PaymentTrace.PaymentMethod);
        Assert.Equal("TRACE-80", body.PaymentTrace.PaymentReference);
        Assert.Equal(1, body.PaymentTrace.PaymentCount);
    }

    [Fact]
    public async Task ListSalesInvoices_SearchByNumber()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-List");

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 1m, unitPrice = 10m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        var listResp = await GetAuth($"/api/v1/sales?q={created!.InvoiceNumber}", token);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<PagedInvoiceResp>(JsonOpts);
        Assert.True(body!.TotalCount >= 1);
        Assert.Contains(body.Items, i => i.InvoiceNumber == created.InvoiceNumber);
    }

    [Fact]
    public async Task UpdateDraftInvoice_ChangesLines()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v1 = await CreateVariantAsync(token, "Sale-Upd-V1");
        var v2 = await CreateVariantAsync(token, "Sale-Upd-V2");

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[] { new { variantId = v1, quantity = 1m, unitPrice = 10m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        // Now update lines
        var updateResp = await PutAuth($"/api/v1/sales/{created!.Id}", token, new
        {
            notes = "Updated",
            lines = new[]
            {
                new { variantId = v2, quantity = 5m, unitPrice = 20m, discountAmount = (decimal?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var body = await updateResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.Equal("Updated", body!.Notes);
        Assert.Single(body.Lines);
        Assert.Equal(v2, body.Lines[0].VariantId);
        Assert.Equal(100m, body.TotalAmount); // 5 * 20
    }

    [Fact]
    public async Task DeleteDraftInvoice_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-Del");

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 1m, unitPrice = 10m, discountAmount = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        var delResp = await DeleteAuth($"/api/v1/sales/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);

        var getResp = await GetAuth($"/api/v1/sales/{created.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task PostInvoice_ReducesStock()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-PostStock");

        // Seed stock: 50 units via opening balance
        await SeedStock(token, variantId, warehouseId, 50m);

        // Create invoice: sell 15 units
        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 15m, unitPrice = 30m, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        // Post the invoice
        var postResp = await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var postBody = await postResp.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(postBody);
        Assert.NotEqual(Guid.Empty, postBody!.StockMovementId);

        // Verify stock: 50 - 15 = 35
        var balance = await GetBalanceAsync(token, warehouseId);
        var varBalance = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(varBalance);
        Assert.Equal(35m, varBalance!.Quantity);
    }

    [Fact]
    public async Task PostInvoice_InsufficientStock_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-NoStock");

        // No stock seeded — balance = 0

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 10m, unitPrice = 50m, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        // Post should fail — insufficient stock
        var postResp = await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, postResp.StatusCode);

        var body = await postResp.Content.ReadAsStringAsync();
        Assert.Contains("STOCK_NEGATIVE_NOT_ALLOWED", body);
    }

    [Fact]
    public async Task PostInvoice_IsIdempotent()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-Idempotent");

        await SeedStock(token, variantId, warehouseId, 100m);

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 5m, unitPrice = 10m, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        // Post twice
        var post1 = await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post1.StatusCode);
        var result1 = await post1.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        var post2 = await PostAuth($"/api/v1/sales/{invoice.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post2.StatusCode);
        var result2 = await post2.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        // Same movement ID both times
        Assert.Equal(result1!.StockMovementId, result2!.StockMovementId);

        // Stock should only be reduced once: 100 - 5 = 95
        var balance = await GetBalanceAsync(token, warehouseId);
        var varBalance = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(varBalance);
        Assert.Equal(95m, varBalance!.Quantity);
    }

    [Fact]
    public async Task PostInvoice_ConcurrentDoublePost_OnlyOneStockMovement()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-ConcPost");

        await SeedStock(token, variantId, warehouseId, 100m);

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 5m, unitPrice = 10m, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        // Fire two parallel post requests
        var task1 = PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        var task2 = PostAuth($"/api/v1/sales/{invoice.Id}/post", token, new { });
        var results = await Task.WhenAll(task1, task2);

        // Both succeed (one real, one idempotent) or one gets 409 (mid-posting)
        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(okCount >= 1, $"At least one post must succeed. Statuses: {string.Join(", ", results.Select(r => r.StatusCode))}");

        foreach (var r in results)
        {
            Assert.True(
                r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict,
                $"Expected 200 or 409 but got {(int)r.StatusCode}");
        }

        // Key invariant: stock reduced exactly once (100 - 5 = 95)
        var balance = await GetBalanceAsync(token, warehouseId);
        var varBalance = balance.FirstOrDefault(b => b.VariantId == variantId);
        Assert.NotNull(varBalance);
        Assert.Equal(95m, varBalance!.Quantity);

        // Entity is posted with a stock movement
        var getResp = await GetAuth($"/api/v1/sales/{invoice.Id}", token);
        var posted = await getResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.Equal("Posted", posted!.Status);
        Assert.NotNull(posted.StockMovementId);
    }

    [Fact]
    public async Task PostInvoice_InvoiceNumberImmutable()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-ImmNum");

        await SeedStock(token, variantId, warehouseId, 100m);

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 1m, unitPrice = 10m, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        var originalNumber = invoice!.InvoiceNumber;

        // Post the invoice
        await PostAuth($"/api/v1/sales/{invoice.Id}/post", token, new { });

        // Re-fetch and confirm number unchanged
        var getResp = await GetAuth($"/api/v1/sales/{invoice.Id}", token);
        var posted = await getResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.Equal(originalNumber, posted!.InvoiceNumber);
        Assert.Equal("Posted", posted.Status);

        // Update should fail on posted invoice
        var updateResp = await PutAuth($"/api/v1/sales/{invoice.Id}", token, new
        {
            notes = "Trying to modify"
        });
        Assert.Equal(HttpStatusCode.Conflict, updateResp.StatusCode);
    }

    [Fact]
    public async Task PostInvoice_WithCustomer_CreatesReceivable()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Sale-Receivable");
        var customerId = await CreateCustomerAsync(token, "RecvTestCust");

        await SeedStock(token, variantId, warehouseId, 100m);

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            customerId,
            lines = new[]
            {
                new { variantId, quantity = 10m, unitPrice = 25m, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        // Post the invoice
        var postResp = await PostAuth($"/api/v1/sales/{invoice!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Verify posted status and total
        var getResp = await GetAuth($"/api/v1/sales/{invoice.Id}", token);
        var posted = await getResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);
        Assert.Equal("Posted", posted!.Status);
        Assert.Equal(250m, posted.TotalAmount); // 10 * 25
        Assert.NotNull(posted.StockMovementId);
    }

    [Fact]
    public async Task PostInvoice_MultiLine_ReducesAllStock()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var v1 = await CreateVariantAsync(token, "Sale-ML-V1");
        var v2 = await CreateVariantAsync(token, "Sale-ML-V2");

        await SeedStock(token, v1, warehouseId, 40m);
        await SeedStock(token, v2, warehouseId, 60m);

        var createResp = await PostAuth("/api/v1/sales", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId = v1, quantity = 10m, unitPrice = 15m, discountAmount = (decimal?)2m },
                new { variantId = v2, quantity = 20m, unitPrice = 8m, discountAmount = (decimal?)null }
            }
        });
        createResp.EnsureSuccessStatusCode();
        var invoice = await createResp.Content.ReadFromJsonAsync<InvoiceResp>(JsonOpts);

        // Total: (10*15 - 2) + (20*8 - 0) = 148 + 160 = 308
        Assert.Equal(308m, invoice!.TotalAmount);

        var postResp = await PostAuth($"/api/v1/sales/{invoice.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var balance = await GetBalanceAsync(token, warehouseId);
        var b1 = balance.FirstOrDefault(b => b.VariantId == v1);
        var b2 = balance.FirstOrDefault(b => b.VariantId == v2);
        Assert.Equal(30m, b1!.Quantity); // 40 - 10
        Assert.Equal(40m, b2!.Quantity); // 60 - 20
    }

    [Fact]
    public async Task BarcodeLookup_ReturnsPricingDefaults()
    {
        var token = await LoginAsAdminAsync();
        var barcode = $"POS-BC-{Guid.NewGuid():N}";
        var sku = $"POS-{Guid.NewGuid():N}";

        // Create product + variant with barcode and pricing
        var prodResp = await PostAuth("/api/v1/products", token, new { name = "POS-Barcode-Test" });
        prodResp.EnsureSuccessStatusCode();
        var product = await prodResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);

        var varResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku,
            barcode,
            color = "Red",
            size = "M",
            retailPrice = 99.99m,
            wholesalePrice = 75.50m
        });
        varResp.EnsureSuccessStatusCode();

        // Lookup by barcode
        var lookupResp = await GetAuth($"/api/v1/barcodes/{barcode}", token);
        Assert.Equal(HttpStatusCode.OK, lookupResp.StatusCode);

        var result = await lookupResp.Content.ReadFromJsonAsync<BarcodeLookupResp>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal(barcode, result!.Barcode);
        Assert.Equal(sku, result.Sku);
        Assert.Equal(99.99m, result.RetailPrice);
        Assert.Equal(75.50m, result.WholesalePrice);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Sales_RequiresAuthentication()
    {
        var resp = await _client.GetAsync("/api/v1/sales");
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

    private async Task<Guid> CreateVariantAsync(string token, string name)
    {
        var prodResp = await PostAuth("/api/v1/products", token, new { name });
        prodResp.EnsureSuccessStatusCode();
        var product = await prodResp.Content.ReadFromJsonAsync<IdResp>(JsonOpts);

        var varResp = await PostAuth("/api/v1/variants", token, new
        {
            productId = product!.Id,
            sku = $"SALE-{Guid.NewGuid():N}",
            barcode = $"SALE-BC-{Guid.NewGuid():N}",
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

    private async Task<Guid> GetDefaultWarehouseIdAsync(string token)
    {
        var resp = await GetAuth("/api/v1/warehouses", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedWarehouseResp>(JsonOpts);
        return body!.Items.First(w => w.IsDefault).Id;
    }

    private async Task SeedStock(string token, Guid variantId, Guid warehouseId, decimal qty)
    {
        var resp = await PostAuth("/api/v1/stock-movements/post", token, new
        {
            type = 0, // OpeningBalance
            reference = $"SALE-SEED-{Guid.NewGuid():N}",
            lines = new[]
            {
                new { variantId, warehouseId, quantityDelta = qty, unitCost = (decimal?)1m, reason = (string?)null }
            }
        });
        resp.EnsureSuccessStatusCode();
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
    private sealed record InvoiceResp(
        Guid Id, string InvoiceNumber, DateTime InvoiceDateUtc,
        Guid? CustomerId, string? CustomerName,
        Guid WarehouseId, string WarehouseName,
        Guid CashierUserId, string CashierUsername,
        string? Notes, string Status,
        Guid? StockMovementId, decimal TotalAmount,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        List<InvoiceLineResp> Lines,
        PaymentTraceResp? PaymentTrace);
    private sealed record InvoiceLineResp(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal UnitPrice,
        decimal DiscountAmount, decimal LineTotal);
    private sealed record PaymentTraceResp(
        decimal? PaidAmount,
        decimal? RemainingAmount,
        string? PaymentMethod,
        string? WalletName,
        string? PaymentReference,
        int PaymentCount);
    private sealed record PostResultResp(Guid StockMovementId);
    private sealed record PagedInvoiceResp(List<InvoiceResp> Items, int TotalCount);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
    private sealed record BarcodeLookupResp(
        string Barcode, string Status,
        Guid VariantId, string Sku, string? Color, string? Size,
        decimal? RetailPrice, decimal? WholesalePrice, bool IsActive,
        Guid ProductId, string ProductName, string? ProductCategory);
}

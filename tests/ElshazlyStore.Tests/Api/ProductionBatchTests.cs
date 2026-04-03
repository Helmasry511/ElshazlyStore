using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for production batch CRUD, posting atomicity (consume + produce),
/// negative stock enforcement, and idempotency.
/// </summary>
[Collection("Integration")]
public sealed class ProductionBatchTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProductionBatchTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProductionBatch_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Create-Raw");
        var finishedId = await CreateVariantAsync(token, "ProdTest-Create-Finished");

        var resp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            notes = "Test batch",
            inputs = new[] { new { variantId = rawId, quantity = 10m, unitCost = (decimal?)5m } },
            outputs = new[] { new { variantId = finishedId, quantity = 2m, unitCost = (decimal?)25m } }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("Draft", body!.Status);
        Assert.Single(body.Inputs);
        Assert.Single(body.Outputs);
        Assert.Equal(10m, body.Inputs[0].Quantity);
        Assert.Equal(2m, body.Outputs[0].Quantity);
    }

    [Fact]
    public async Task PostProductionBatch_ConsumesAndProduces()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Post-Raw");
        var finishedId = await CreateVariantAsync(token, "ProdTest-Post-Finished");

        // Stock raw materials first via purchase
        await StockRawMaterial(token, rawId, warehouseId, 100m);

        // Create production batch: consume 40 raw → produce 8 finished
        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[] { new { variantId = rawId, quantity = 40m, unitCost = (decimal?)5m } },
            outputs = new[] { new { variantId = finishedId, quantity = 8m, unitCost = (decimal?)25m } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        // Post the batch
        var postResp = await PostAuth($"/api/v1/production/{batch!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var postBody = await postResp.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(postBody);
        Assert.NotEqual(Guid.Empty, postBody!.ConsumeMovementId);
        Assert.NotEqual(Guid.Empty, postBody.ProduceMovementId);

        // Verify raw material decreased: 100 - 40 = 60
        var balance = await GetBalanceAsync(token, warehouseId);
        var rawBalance = balance.FirstOrDefault(b => b.VariantId == rawId);
        Assert.NotNull(rawBalance);
        Assert.Equal(60m, rawBalance!.Quantity);

        // Verify finished goods increased: 0 + 8 = 8
        var finishedBalance = balance.FirstOrDefault(b => b.VariantId == finishedId);
        Assert.NotNull(finishedBalance);
        Assert.Equal(8m, finishedBalance!.Quantity);
    }

    [Fact]
    public async Task PostProductionBatch_InsufficientRawMaterial_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-NoStock-Raw");
        var finishedId = await CreateVariantAsync(token, "ProdTest-NoStock-Finished");

        // No stock added for raw material — balance is 0

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[] { new { variantId = rawId, quantity = 50m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finishedId, quantity = 10m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        // Post should fail — insufficient raw materials
        var postResp = await PostAuth($"/api/v1/production/{batch!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, postResp.StatusCode);

        var body = await postResp.Content.ReadAsStringAsync();
        Assert.Contains("STOCK_NEGATIVE_NOT_ALLOWED", body);
    }

    [Fact]
    public async Task PostProductionBatch_IsIdempotent()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Idemp-Raw");
        var finishedId = await CreateVariantAsync(token, "ProdTest-Idemp-Finished");

        await StockRawMaterial(token, rawId, warehouseId, 200m);

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[] { new { variantId = rawId, quantity = 20m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finishedId, quantity = 5m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        // Post first time
        var post1 = await PostAuth($"/api/v1/production/{batch!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post1.StatusCode);
        var body1 = await post1.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        // Post second time — idempotent
        var post2 = await PostAuth($"/api/v1/production/{batch.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post2.StatusCode);
        var body2 = await post2.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        Assert.Equal(body1!.ConsumeMovementId, body2!.ConsumeMovementId);
        Assert.Equal(body1.ProduceMovementId, body2.ProduceMovementId);

        // Verify balance not doubled: raw = 200 - 20 = 180 (not 160)
        var balance = await GetBalanceAsync(token, warehouseId);
        Assert.Equal(180m, balance.First(b => b.VariantId == rawId).Quantity);
        Assert.Equal(5m, balance.First(b => b.VariantId == finishedId).Quantity);
    }

    [Fact]
    public async Task PostProductionBatch_ConcurrentDoublePost_OnlyOneStockMovement()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Conc-Raw");
        var finId = await CreateVariantAsync(token, "ProdTest-Conc-Fin");

        await StockRawMaterial(token, rawId, warehouseId, 200m);

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[] { new { variantId = rawId, quantity = 20m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finId, quantity = 5m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        // Fire two parallel post requests
        var task1 = PostAuth($"/api/v1/production/{batch!.Id}/post", token, new { });
        var task2 = PostAuth($"/api/v1/production/{batch.Id}/post", token, new { });
        var results = await Task.WhenAll(task1, task2);

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(okCount >= 1, $"At least one post must succeed. Statuses: {string.Join(", ", results.Select(r => r.StatusCode))}");

        foreach (var r in results)
        {
            Assert.True(
                r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict,
                $"Expected 200 or 409 but got {(int)r.StatusCode}");
        }

        // Key invariant: stock consumed/produced exactly once
        var balance = await GetBalanceAsync(token, warehouseId);
        Assert.Equal(180m, balance.First(b => b.VariantId == rawId).Quantity);  // 200 - 20
        Assert.Equal(5m, balance.First(b => b.VariantId == finId).Quantity);

        var getResp = await GetAuth($"/api/v1/production/{batch.Id}", token);
        var posted = await getResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);
        Assert.Equal("Posted", posted!.Status);
        Assert.NotNull(posted.ConsumeMovementId);
        Assert.NotNull(posted.ProduceMovementId);
    }

    [Fact]
    public async Task PostProductionBatch_MultipleInputsAndOutputs()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var raw1 = await CreateVariantAsync(token, "ProdTest-Multi-Raw1");
        var raw2 = await CreateVariantAsync(token, "ProdTest-Multi-Raw2");
        var fin1 = await CreateVariantAsync(token, "ProdTest-Multi-Fin1");
        var fin2 = await CreateVariantAsync(token, "ProdTest-Multi-Fin2");

        await StockRawMaterial(token, raw1, warehouseId, 300m);
        await StockRawMaterial(token, raw2, warehouseId, 500m);

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[]
            {
                new { variantId = raw1, quantity = 100m, unitCost = (decimal?)2m },
                new { variantId = raw2, quantity = 200m, unitCost = (decimal?)3m },
            },
            outputs = new[]
            {
                new { variantId = fin1, quantity = 50m, unitCost = (decimal?)10m },
                new { variantId = fin2, quantity = 25m, unitCost = (decimal?)20m },
            }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/production/{batch!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var balance = await GetBalanceAsync(token, warehouseId);
        Assert.Equal(200m, balance.First(b => b.VariantId == raw1).Quantity);  // 300 - 100
        Assert.Equal(300m, balance.First(b => b.VariantId == raw2).Quantity);  // 500 - 200
        Assert.Equal(50m, balance.First(b => b.VariantId == fin1).Quantity);
        Assert.Equal(25m, balance.First(b => b.VariantId == fin2).Quantity);
    }

    [Fact]
    public async Task GetProductionBatch_ReturnsDetails()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Get-Raw");
        var finId = await CreateVariantAsync(token, "ProdTest-Get-Fin");

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            batchNumber = $"PB-GET-{Guid.NewGuid():N}",
            notes = "Get test",
            inputs = new[] { new { variantId = rawId, quantity = 5m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finId, quantity = 1m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        var getResp = await GetAuth($"/api/v1/production/{batch!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var body = await getResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(batch.Id, body!.Id);
        Assert.Equal("Get test", body.Notes);
    }

    [Fact]
    public async Task ListProductionBatches_WithSearch()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Search-Raw");
        var finId = await CreateVariantAsync(token, "ProdTest-Search-Fin");

        var batchNum = $"PB-SEARCH-{Guid.NewGuid():N}";
        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            batchNumber = batchNum,
            inputs = new[] { new { variantId = rawId, quantity = 5m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finId, quantity = 1m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();

        var listResp = await GetAuth($"/api/v1/production?q={batchNum}", token);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<PagedBatchResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 1);
        Assert.Contains(body.Items, b => b.BatchNumber == batchNum);
    }

    [Fact]
    public async Task UpdateProductionBatch_DraftOnly()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Update-Raw");
        var finId = await CreateVariantAsync(token, "ProdTest-Update-Fin");

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[] { new { variantId = rawId, quantity = 5m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finId, quantity = 1m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        // Update notes on draft
        var updateResp = await PutAuth($"/api/v1/production/{batch!.Id}", token, new
        {
            notes = "Updated notes"
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Stock raw material and post
        await StockRawMaterial(token, rawId, warehouseId, 100m);
        await PostAuth($"/api/v1/production/{batch.Id}/post", token, new { });

        // Attempt to update posted batch — should fail
        var updateAfterPost = await PutAuth($"/api/v1/production/{batch.Id}", token, new
        {
            notes = "Should fail"
        });
        Assert.Equal(HttpStatusCode.Conflict, updateAfterPost.StatusCode);
    }

    [Fact]
    public async Task DeleteProductionBatch_DraftOnly()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-Del-Raw");
        var finId = await CreateVariantAsync(token, "ProdTest-Del-Fin");

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[] { new { variantId = rawId, quantity = 5m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finId, quantity = 1m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        var delResp = await DeleteAuth($"/api/v1/production/{batch!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);

        var getResp = await GetAuth($"/api/v1/production/{batch.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task DeleteProductionBatch_PostedFails()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var rawId = await CreateVariantAsync(token, "ProdTest-DelPost-Raw");
        var finId = await CreateVariantAsync(token, "ProdTest-DelPost-Fin");

        await StockRawMaterial(token, rawId, warehouseId, 100m);

        var createResp = await PostAuth("/api/v1/production", token, new
        {
            warehouseId,
            inputs = new[] { new { variantId = rawId, quantity = 10m, unitCost = (decimal?)null } },
            outputs = new[] { new { variantId = finId, quantity = 2m, unitCost = (decimal?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var batch = await createResp.Content.ReadFromJsonAsync<BatchResp>(JsonOpts);

        await PostAuth($"/api/v1/production/{batch!.Id}/post", token, new { });

        var delResp = await DeleteAuth($"/api/v1/production/{batch.Id}", token);
        Assert.Equal(HttpStatusCode.Conflict, delResp.StatusCode);
    }

    [Fact]
    public async Task Production_RequiresAuthentication()
    {
        var resp = await _client.GetAsync("/api/v1/production");
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
            sku = $"PROD-{Guid.NewGuid():N}",
            barcode = $"PROD-BC-{Guid.NewGuid():N}",
            color = "Blue",
            size = "S",
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

    /// <summary>Add stock via direct stock movement (simulates purchase).</summary>
    private async Task StockRawMaterial(string token, Guid variantId, Guid warehouseId, decimal qty)
    {
        var resp = await PostAuth("/api/v1/stock-movements/post", token, new
        {
            type = 0, // OpeningBalance
            reference = $"PROD-SEED-{Guid.NewGuid():N}",
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
    private sealed record BatchResp(
        Guid Id, string BatchNumber,
        Guid WarehouseId, string WarehouseName, string? Notes,
        string Status, Guid? ConsumeMovementId, Guid? ProduceMovementId,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        List<BatchLineResp> Inputs, List<BatchLineResp> Outputs);
    private sealed record BatchLineResp(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal? UnitCost);
    private sealed record PostResultResp(Guid ConsumeMovementId, Guid ProduceMovementId);
    private sealed record PagedBatchResp(List<BatchResp> Items, int TotalCount);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
}

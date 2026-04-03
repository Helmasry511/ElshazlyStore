using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElshazlyStore.Tests.Api;

/// <summary>
/// Tests for Phase RET 3: Pre-sale Dispositions (Damage/Theft/Defects).
/// Covers:
/// - Create draft disposition (Scrap, Quarantine, WriteOff, Rework)
/// - Post disposition creates correct stock movement types/warehouses
///   - Scrap: transfer to SCRAP warehouse
///   - Quarantine: transfer to QUARANTINE warehouse
///   - WriteOff: remove from source (no destination)
///   - Rework: transfer to REWORK warehouse
/// - Manager approval required when reason has RequiresManagerApproval
/// - Posting without approval is rejected
/// - Negative stock prevention
/// - Reason code must be active
/// - Void draft disposition
/// - Cannot void posted disposition
/// - Get by ID
/// - List/search
/// - Update draft lines (clears approval)
/// - Delete draft
/// - Requires authentication
/// - Invalid disposition types (ReturnToVendor, ReturnToStock) rejected
/// </summary>
[Collection("Integration")]
public sealed class DispositionTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DispositionTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ───── Create Tests ─────

    [Fact]
    public async Task CreateDisposition_Scrap_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Scrap");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_SC_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 50m);

        var resp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            notes = "Damaged goods - scrap",
            lines = new[]
            {
                new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 0 /* Scrap */, notes = "Broken packaging" }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal("Draft", body!.Status);
        Assert.StartsWith("DISP-", body.DispositionNumber);
        Assert.Single(body.Lines);
        Assert.Equal("Scrap", body.Lines[0].DispositionType);
        Assert.Equal(5m, body.Lines[0].Quantity);
    }

    [Fact]
    public async Task CreateDisposition_Quarantine_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Quarantine");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_QR_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 30m);

        var resp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 10m, reasonCodeId = reasonId, dispositionType = 4 /* Quarantine */, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.Equal("Quarantine", body!.Lines[0].DispositionType);
    }

    [Fact]
    public async Task CreateDisposition_InvalidType_ReturnToVendor_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Invalid");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_IV_{Guid.NewGuid():N}"[..30], false);

        var resp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[]
            {
                new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 2 /* ReturnToVendor */, notes = (string?)null }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("DISPOSITION_INVALID_TYPE", problem!.Title);
    }

    // ───── Post Tests ─────

    [Fact]
    public async Task PostDisposition_Scrap_MovesStockToScrapWarehouse()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var scrapWarehouseId = await GetWarehouseByCodeAsync(token, "SCRAP");
        var variantId = await CreateVariantAsync(token, "Disp-PostScrap");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_PS_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 100m);

        // Create disposition for 10 units to Scrap
        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 10m, reasonCodeId = reasonId, dispositionType = 0 /* Scrap */, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // Post the disposition
        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
        var postBody = await postResp.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(postBody?.StockMovementId);

        // Check source warehouse balance: 100 - 10 = 90
        var sourceBalance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(90m, sourceBalance);

        // Check scrap warehouse balance: 0 + 10 = 10
        var scrapBalance = await GetBalanceForVariant(token, scrapWarehouseId, variantId);
        Assert.Equal(10m, scrapBalance);
    }

    [Fact]
    public async Task PostDisposition_Quarantine_MovesStockToQuarantineWarehouse()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var quarantineWarehouseId = await GetWarehouseByCodeAsync(token, "QUARANTINE");
        var variantId = await CreateVariantAsync(token, "Disp-PostQuar");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_PQ_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 80m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 15m, reasonCodeId = reasonId, dispositionType = 4 /* Quarantine */, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Source: 80 - 15 = 65
        var sourceBalance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(65m, sourceBalance);

        // Quarantine: 0 + 15 = 15
        var quarantineBalance = await GetBalanceForVariant(token, quarantineWarehouseId, variantId);
        Assert.Equal(15m, quarantineBalance);
    }

    [Fact]
    public async Task PostDisposition_WriteOff_RemovesStockNoDest()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-PostWO");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_WO_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 8m, reasonCodeId = reasonId, dispositionType = 5 /* WriteOff */, notes = "Theft" } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Source: 50 - 8 = 42
        var sourceBalance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(42m, sourceBalance);
    }

    [Fact]
    public async Task PostDisposition_Rework_MovesStockToReworkWarehouse()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var reworkWarehouseId = await GetWarehouseByCodeAsync(token, "REWORK");
        var variantId = await CreateVariantAsync(token, "Disp-PostRework");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_RW_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 60m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 12m, reasonCodeId = reasonId, dispositionType = 1 /* Rework */, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // Source: 60 - 12 = 48
        var sourceBalance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(48m, sourceBalance);

        // Rework: 0 + 12 = 12
        var reworkBalance = await GetBalanceForVariant(token, reworkWarehouseId, variantId);
        Assert.Equal(12m, reworkBalance);
    }

    // ───── Double Post / Idempotency ─────

    [Fact]
    public async Task PostDisposition_DoublePostIsIdempotent()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-DoublePost");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_DP_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 10m, reasonCodeId = reasonId, dispositionType = 5 /* WriteOff */, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // First post
        var post1 = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post1.StatusCode);
        var body1 = await post1.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(body1?.StockMovementId);

        // Second post — should succeed idempotently
        var post2 = await PostAuth($"/api/v1/dispositions/{disp.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, post2.StatusCode);
        var body2 = await post2.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);

        // Same stock movement ID returned on both calls
        Assert.Equal(body1!.StockMovementId, body2!.StockMovementId);

        // Stock should only decrease once: 50 - 10 = 40
        var sourceBalance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(40m, sourceBalance);
    }

    // ───── Negative Stock ─────

    [Fact]
    public async Task PostDisposition_NegativeStockPrevented()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-NegStock");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_NG_{Guid.NewGuid():N}"[..30], false);

        // Seed only 5 units
        await SeedStock(token, variantId, warehouseId, 5m);

        // Try to dispose 10 (more than available)
        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 10m, reasonCodeId = reasonId, dispositionType = 5 /* WriteOff */, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal((HttpStatusCode)422, postResp.StatusCode);
        var problem = await postResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("STOCK_NEGATIVE_NOT_ALLOWED", problem!.Title);
    }

    // ───── Manager Approval Tests ─────

    [Fact]
    public async Task PostDisposition_RequiresManagerApproval_RejectedWithoutApproval()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Approval");
        // Create a reason that requires manager approval
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_AP_{Guid.NewGuid():N}"[..30], true);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 5 /* WriteOff */, notes = "Theft - needs approval" } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // Try to post without approval
        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal((HttpStatusCode)403, postResp.StatusCode);
        var problem = await postResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("DISPOSITION_REQUIRES_APPROVAL", problem!.Title);
    }

    [Fact]
    public async Task PostDisposition_WithApproval_SucceedsAfterApprove()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-ApprovePost");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_AO_{Guid.NewGuid():N}"[..30], true);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 5 /* WriteOff */, notes = "Theft - approved" } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // Approve first
        var approveResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/approve", token, new { });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        // Verify approval fields on get
        var getResp = await GetAuth($"/api/v1/dispositions/{disp.Id}", token);
        var approved = await getResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.NotNull(approved!.ApprovedByUserId);
        Assert.NotNull(approved.ApprovedAtUtc);

        // Now post should succeed
        var postResp = await PostAuth($"/api/v1/dispositions/{disp.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var postBody = await postResp.Content.ReadFromJsonAsync<PostResultResp>(JsonOpts);
        Assert.NotNull(postBody?.StockMovementId);

        // Verify stock: 50 - 5 = 45
        var balance = await GetBalanceForVariant(token, warehouseId, variantId);
        Assert.Equal(45m, balance);
    }

    [Fact]
    public async Task PostDisposition_MixedLines_ApprovalRequiredIfAnyLineNeedsIt()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Mixed");
        var normalReasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_MN_{Guid.NewGuid():N}"[..30], false);
        var approvalReasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_MA_{Guid.NewGuid():N}"[..30], true);

        await SeedStock(token, variantId, warehouseId, 100m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new object[]
            {
                new { variantId, quantity = 5m, reasonCodeId = normalReasonId, dispositionType = 0 /* Scrap */, notes = (string?)null },
                new { variantId, quantity = 3m, reasonCodeId = approvalReasonId, dispositionType = 5 /* WriteOff */, notes = "Needs approval" },
            }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // Post without approval — should fail
        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal((HttpStatusCode)403, postResp.StatusCode);

        // Approve and then post
        await PostAuth($"/api/v1/dispositions/{disp.Id}/approve", token, new { });
        var postResp2 = await PostAuth($"/api/v1/dispositions/{disp.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.OK, postResp2.StatusCode);
    }

    // ───── Reason Code Validation ─────

    [Fact]
    public async Task PostDisposition_InactiveReasonCode_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-InactiveRC");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_IR_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 0, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // Deactivate the reason code
        var disableResp = await PostAuth($"/api/v1/reasons/{reasonId}/disable", token, new { });
        disableResp.EnsureSuccessStatusCode();

        // Post should fail
        var postResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });
        Assert.Equal(HttpStatusCode.BadRequest, postResp.StatusCode);
        var problem = await postResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("REASON_CODE_INACTIVE", problem!.Title);
    }

    // ───── Void Tests ─────

    [Fact]
    public async Task VoidDraftDisposition_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Void");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_VD_{Guid.NewGuid():N}"[..30], false);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 0, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var voidResp = await PostAuth($"/api/v1/dispositions/{disp!.Id}/void", token, new { });
        Assert.Equal(HttpStatusCode.OK, voidResp.StatusCode);

        // Verify status
        var getResp = await GetAuth($"/api/v1/dispositions/{disp.Id}", token);
        var voided = await getResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.Equal("Voided", voided!.Status);
    }

    [Fact]
    public async Task VoidPostedDisposition_Rejected()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-VoidPosted");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_VP_{Guid.NewGuid():N}"[..30], false);

        await SeedStock(token, variantId, warehouseId, 50m);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 5 /* WriteOff */, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var disp = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // Post it
        await PostAuth($"/api/v1/dispositions/{disp!.Id}/post", token, new { });

        // Try to void — should fail
        var voidResp = await PostAuth($"/api/v1/dispositions/{disp.Id}/void", token, new { });
        Assert.Equal((HttpStatusCode)409, voidResp.StatusCode);
        var problem = await voidResp.Content.ReadFromJsonAsync<ProblemResp>(JsonOpts);
        Assert.Equal("DISPOSITION_VOID_NOT_ALLOWED_AFTER_POST", problem!.Title);
    }

    // ───── CRUD Tests ─────

    [Fact]
    public async Task GetDisposition_ReturnsDetails()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Get");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_GT_{Guid.NewGuid():N}"[..30], false);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            notes = "Get test",
            lines = new[] { new { variantId, quantity = 3m, reasonCodeId = reasonId, dispositionType = 0, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var getResp = await GetAuth($"/api/v1/dispositions/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var body = await getResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("Get test", body.Notes);
    }

    [Fact]
    public async Task ListDispositions_ReturnsPaged()
    {
        var token = await LoginAsAdminAsync();
        var resp = await GetAuth("/api/v1/dispositions?pageSize=5", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<PagedDispositionResp>(JsonOpts);
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 0);
    }

    [Fact]
    public async Task UpdateDraftDisposition_UpdatesLines()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Update");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_UD_{Guid.NewGuid():N}"[..30], false);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 1m, reasonCodeId = reasonId, dispositionType = 0, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var updateResp = await PutAuth($"/api/v1/dispositions/{created!.Id}", token, new
        {
            notes = "Updated notes",
            lines = new[]
            {
                new { variantId, quantity = 7m, reasonCodeId = reasonId, dispositionType = 4 /* Quarantine */, notes = "Updated line" }
            }
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.Equal(7m, updated!.Lines[0].Quantity);
        Assert.Equal("Quarantine", updated.Lines[0].DispositionType);
        Assert.Equal("Updated notes", updated.Notes);
    }

    [Fact]
    public async Task UpdateDraftDisposition_ClearsApproval()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-UpdAppr");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_UA_{Guid.NewGuid():N}"[..30], true);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 5m, reasonCodeId = reasonId, dispositionType = 5, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        // Approve
        await PostAuth($"/api/v1/dispositions/{created!.Id}/approve", token, new { });

        // Verify approved
        var getResp = await GetAuth($"/api/v1/dispositions/{created.Id}", token);
        var approved = await getResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.NotNull(approved!.ApprovedByUserId);

        // Update lines — should clear approval
        var updateResp = await PutAuth($"/api/v1/dispositions/{created.Id}", token, new
        {
            lines = new[] { new { variantId, quantity = 3m, reasonCodeId = reasonId, dispositionType = 5, notes = (string?)null } }
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);
        Assert.Null(updated!.ApprovedByUserId);
        Assert.Null(updated.ApprovedAtUtc);
    }

    [Fact]
    public async Task DeleteDraftDisposition_Succeeds()
    {
        var token = await LoginAsAdminAsync();
        var warehouseId = await GetDefaultWarehouseIdAsync(token);
        var variantId = await CreateVariantAsync(token, "Disp-Del");
        var reasonId = await CreateDispositionReasonCodeAsync(token, $"DISP_DL_{Guid.NewGuid():N}"[..30], false);

        var createResp = await PostAuth("/api/v1/dispositions", token, new
        {
            warehouseId,
            lines = new[] { new { variantId, quantity = 1m, reasonCodeId = reasonId, dispositionType = 0, notes = (string?)null } }
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<DispositionResp>(JsonOpts);

        var deleteResp = await DeleteAuth($"/api/v1/dispositions/{created!.Id}", token);
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        var getResp = await GetAuth($"/api/v1/dispositions/{created.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Dispositions_RequiresAuthentication()
    {
        var resp = await _client.GetAsync("/api/v1/dispositions");
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
            sku = $"DISP-{Guid.NewGuid():N}",
            barcode = $"DISP-BC-{Guid.NewGuid():N}",
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
        var body = await resp.Content.ReadFromJsonAsync<PagedWarehouseResp>(JsonOpts);
        return body!.Items.First(w => w.IsDefault).Id;
    }

    private async Task<Guid> GetWarehouseByCodeAsync(string token, string code)
    {
        var resp = await GetAuth("/api/v1/warehouses", token);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PagedWarehouseResp>(JsonOpts);
        return body!.Items.First(w => w.Code == code).Id;
    }

    private async Task<Guid> CreateDispositionReasonCodeAsync(string token, string code, bool requiresApproval)
    {
        var resp = await PostAuth("/api/v1/reasons", token, new
        {
            code,
            nameAr = "سبب تصرف مخزني",
            category = "Disposition",
            requiresManagerApproval = requiresApproval
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
            reference = $"DISP-SEED-{Guid.NewGuid():N}",
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
    private sealed record DispositionResp(
        Guid Id, string DispositionNumber, DateTime DispositionDateUtc,
        Guid WarehouseId, string WarehouseName,
        Guid CreatedByUserId, string CreatedByUsername,
        string? Notes, string Status,
        Guid? StockMovementId,
        Guid? ApprovedByUserId, string? ApprovedByUsername,
        DateTime? ApprovedAtUtc,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        Guid? PostedByUserId,
        List<DispositionLineResp> Lines);
    private sealed record DispositionLineResp(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity,
        Guid ReasonCodeId, string ReasonCodeCode, string ReasonCodeNameAr,
        bool RequiresManagerApproval,
        string DispositionType, string? Notes);
    private sealed record PostResultResp(Guid? StockMovementId);
    private sealed record ProblemResp(string Title, string Detail);
    private sealed record PagedDispositionResp(List<DispositionResp> Items, int TotalCount);
    private sealed record BalanceItemResp(Guid VariantId, string Sku, decimal Quantity);
    private sealed record PagedBalanceResp(List<BalanceItemResp> Items, int TotalCount);
    private sealed record PagedWarehouseResp(List<WarehouseItemResp> Items, int TotalCount);
    private sealed record WarehouseItemResp(Guid Id, string Code, string Name, bool IsDefault);
}

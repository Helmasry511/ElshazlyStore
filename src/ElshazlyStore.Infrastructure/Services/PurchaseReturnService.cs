using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles purchase return CRUD, posting, and voiding.
/// Posting creates a PurchaseReturnIssue stock movement (negative deltas = stock out)
/// and creates a DebitNote ledger entry for AP (reduces supplier payable).
/// </summary>
public sealed class PurchaseReturnService
{
    private readonly AppDbContext _db;
    private readonly StockService _stockService;
    private readonly AccountingService _accountingService;

    public PurchaseReturnService(AppDbContext db, StockService stockService, AccountingService accountingService)
    {
        _db = db;
        _stockService = stockService;
        _accountingService = accountingService;
    }

    // ───── DTOs ─────

    public sealed record CreateReturnRequest(
        Guid SupplierId,
        Guid WarehouseId,
        Guid? OriginalPurchaseReceiptId,
        DateTime? ReturnDateUtc,
        string? Notes,
        List<ReturnLineRequest> Lines);

    public sealed record ReturnLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal UnitCost,
        Guid ReasonCodeId,
        DispositionType DispositionType,
        string? Notes);

    public sealed record UpdateReturnRequest(
        Guid? SupplierId,
        Guid? WarehouseId,
        Guid? OriginalPurchaseReceiptId,
        bool ClearOriginalReceipt,
        string? Notes,
        List<ReturnLineRequest>? Lines);

    public sealed record ReturnDto(
        Guid Id, string ReturnNumber,
        DateTime ReturnDateUtc,
        Guid SupplierId, string SupplierName,
        Guid? OriginalPurchaseReceiptId, string? OriginalDocumentNumber,
        Guid WarehouseId, string WarehouseName,
        Guid CreatedByUserId, string CreatedByUsername,
        string? Notes, string Status,
        Guid? StockMovementId,
        decimal TotalAmount,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        Guid? PostedByUserId,
        List<ReturnLineDto> Lines);

    public sealed record ReturnLineDto(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal UnitCost, decimal LineTotal,
        Guid ReasonCodeId, string ReasonCodeCode, string ReasonCodeNameAr,
        string DispositionType, string? Notes);

    public sealed record PostResult(
        bool Success, Guid? StockMovementId,
        string? ErrorCode, string? ErrorDetail);

    // ───── Create ─────

    public async Task<(ReturnDto? Return, string? ErrorCode, string? ErrorDetail)> CreateAsync(
        CreateReturnRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Lines is not { Count: > 0 })
            return (null, ErrorCodes.PurchaseReturnEmpty, "Purchase return must have at least one line.");

        // Validate supplier
        var supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct);
        if (supplier is null)
            return (null, ErrorCodes.SupplierNotFound, "Supplier not found.");

        // Validate warehouse
        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive, ct);
        if (warehouse is null)
            return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");

        // Validate original purchase receipt if provided
        string? originalDocNumber = null;
        if (request.OriginalPurchaseReceiptId.HasValue)
        {
            var origReceipt = await _db.PurchaseReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.Id == request.OriginalPurchaseReceiptId.Value, ct);
            if (origReceipt is null)
                return (null, ErrorCodes.PurchaseReceiptNotFound, "Original purchase receipt not found.");
            if (origReceipt.Status != PurchaseReceiptStatus.Posted)
                return (null, ErrorCodes.ValidationFailed, "Original purchase receipt must be posted.");
            originalDocNumber = origReceipt.DocumentNumber;
        }

        // Validate all variants exist
        var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var existingVariants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var missing = variantIds.Except(existingVariants).ToList();
        if (missing.Count > 0)
            return (null, ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

        // Validate reason codes
        var reasonCodeIds = request.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var activeReasonCodes = await _db.ReasonCodes
            .Where(rc => reasonCodeIds.Contains(rc.Id))
            .ToListAsync(ct);
        var missingReasons = reasonCodeIds.Except(activeReasonCodes.Select(rc => rc.Id)).ToList();
        if (missingReasons.Count > 0)
            return (null, ErrorCodes.ReasonCodeNotFound, $"Reason code(s) not found: {string.Join(", ", missingReasons)}");
        var inactiveReasons = activeReasonCodes.Where(rc => !rc.IsActive).ToList();
        if (inactiveReasons.Count > 0)
            return (null, ErrorCodes.ReasonCodeInactive, $"Reason code(s) inactive: {string.Join(", ", inactiveReasons.Select(r => r.Code))}");

        // Validate positive quantities/costs
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
            if (line.UnitCost < 0)
                return (null, ErrorCodes.ValidationFailed, "Line unit cost cannot be negative.");
        }

        // Validate return qty vs received qty if original receipt provided
        if (request.OriginalPurchaseReceiptId.HasValue)
        {
            var validationError = await ValidateReturnQtyAsync(
                request.OriginalPurchaseReceiptId.Value, request.Lines, null, ct);
            if (validationError is not null)
                return (null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);
        }

        // Generate return number: PRET-NNNNNN
        string returnNumber;
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            await EnsureReturnSequenceAsync(ct);
            var seq = await _db.Database
                .SqlQueryRaw<long>("SELECT nextval('purchase_return_number_seq') AS \"Value\"")
                .SingleAsync(ct);
            returnNumber = $"PRET-{seq:D6}";
        }
        else
        {
            // Fallback for SQLite (tests)
            var existingCount = await _db.PurchaseReturns
                .Where(pr => pr.ReturnNumber.StartsWith("PRET-")
                    && pr.ReturnNumber.Length == 11)
                .CountAsync(ct);
            returnNumber = $"PRET-{existingCount + 1:D6}";
            while (await _db.PurchaseReturns.AnyAsync(pr => pr.ReturnNumber == returnNumber, ct))
            {
                existingCount++;
                returnNumber = $"PRET-{existingCount + 1:D6}";
            }
        }

        var purchaseReturn = new PurchaseReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = returnNumber,
            ReturnDateUtc = request.ReturnDateUtc ?? DateTime.UtcNow,
            SupplierId = request.SupplierId,
            OriginalPurchaseReceiptId = request.OriginalPurchaseReceiptId,
            WarehouseId = request.WarehouseId,
            Notes = request.Notes,
            Status = PurchaseReturnStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        decimal totalAmount = 0m;
        foreach (var line in request.Lines)
        {
            var lineTotal = line.Quantity * line.UnitCost;
            purchaseReturn.Lines.Add(new PurchaseReturnLine
            {
                Id = Guid.NewGuid(),
                PurchaseReturnId = purchaseReturn.Id,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                LineTotal = lineTotal,
                ReasonCodeId = line.ReasonCodeId,
                DispositionType = line.DispositionType,
                Notes = line.Notes,
            });
            totalAmount += lineTotal;
        }
        purchaseReturn.TotalAmount = totalAmount;

        _db.PurchaseReturns.Add(purchaseReturn);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FindAsync([userId], ct);
        var variants = await LoadVariantDictAsync(variantIds, ct);
        var reasonDict = activeReasonCodes.ToDictionary(rc => rc.Id);

        return (MapToDto(purchaseReturn, supplier.Name, originalDocNumber,
            warehouse.Name, user!.Username, variants, reasonDict), null, null);
    }

    // ───── Get ─────

    public async Task<ReturnDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseReturns
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .Include(r => r.OriginalPurchaseReceipt)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (pr is null) return null;

        var variantIds = pr.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await LoadVariantDictAsync(variantIds, ct);
        var reasonIds = pr.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var reasons = await LoadReasonCodeDictAsync(reasonIds, ct);

        return MapToDto(pr, pr.Supplier.Name, pr.OriginalPurchaseReceipt?.DocumentNumber,
            pr.Warehouse.Name, pr.CreatedBy.Username, variants, reasons);
    }

    // ───── List / Search ─────

    public async Task<(List<ReturnDto> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize, string? sort,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.PurchaseReturns
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .Include(r => r.OriginalPurchaseReceipt)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .AsNoTracking()
            .AsQueryable();

        query = query.ApplySearch(_db.Database, search,
            r => r.ReturnNumber,
            r => r.Supplier.Name,
            r => r.Supplier.Code,
            r => r.CreatedBy.Username,
            r => r.Notes);

        query = sort?.ToLowerInvariant() switch
        {
            "number" => query.OrderBy(r => r.ReturnNumber),
            "number_desc" => query.OrderByDescending(r => r.ReturnNumber),
            "date" => query.OrderBy(r => r.ReturnDateUtc),
            "date_desc" => query.OrderByDescending(r => r.ReturnDateUtc),
            "total" => query.OrderBy(r => r.TotalAmount),
            "total_desc" => query.OrderByDescending(r => r.TotalAmount),
            "status" => query.OrderBy(r => r.Status),
            "created" => query.OrderBy(r => r.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(r => r.CreatedAtUtc),
            _ => query.OrderByDescending(r => r.CreatedAtUtc),
        };

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var allVariantIds = items.SelectMany(r => r.Lines).Select(l => l.VariantId).Distinct().ToList();
        var allVariants = await LoadVariantDictAsync(allVariantIds, ct);
        var allReasonIds = items.SelectMany(r => r.Lines).Select(l => l.ReasonCodeId).Distinct().ToList();
        var allReasons = await LoadReasonCodeDictAsync(allReasonIds, ct);

        var dtos = items.Select(pr => MapToDto(
            pr, pr.Supplier.Name, pr.OriginalPurchaseReceipt?.DocumentNumber,
            pr.Warehouse.Name, pr.CreatedBy.Username, allVariants, allReasons)).ToList();
        return (dtos, totalCount);
    }

    // ───── Update (Draft only) ─────

    public async Task<(ReturnDto? Return, string? ErrorCode, string? ErrorDetail)> UpdateAsync(
        Guid id, UpdateReturnRequest request, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseReturns
            .Include(r => r.Supplier)
            .Include(r => r.OriginalPurchaseReceipt)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (pr is null)
            return (null, ErrorCodes.PurchaseReturnNotFound, "Purchase return not found.");

        if (pr.Status != PurchaseReturnStatus.Draft)
            return (null, ErrorCodes.PurchaseReturnAlreadyPosted, "Cannot modify a posted or voided purchase return.");

        if (request.SupplierId.HasValue)
        {
            var supplier = await _db.Suppliers
                .FirstOrDefaultAsync(s => s.Id == request.SupplierId.Value, ct);
            if (supplier is null)
                return (null, ErrorCodes.SupplierNotFound, "Supplier not found.");
            pr.SupplierId = request.SupplierId.Value;
            pr.Supplier = supplier;
        }

        if (request.WarehouseId.HasValue)
        {
            var warehouse = await _db.Warehouses
                .FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value && w.IsActive, ct);
            if (warehouse is null)
                return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");
            pr.WarehouseId = request.WarehouseId.Value;
            pr.Warehouse = warehouse;
        }

        if (request.ClearOriginalReceipt)
        {
            pr.OriginalPurchaseReceiptId = null;
            pr.OriginalPurchaseReceipt = null;
        }
        else if (request.OriginalPurchaseReceiptId.HasValue)
        {
            var origReceipt = await _db.PurchaseReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.OriginalPurchaseReceiptId.Value, ct);
            if (origReceipt is null)
                return (null, ErrorCodes.PurchaseReceiptNotFound, "Original purchase receipt not found.");
            if (origReceipt.Status != PurchaseReceiptStatus.Posted)
                return (null, ErrorCodes.ValidationFailed, "Original purchase receipt must be posted.");
            pr.OriginalPurchaseReceiptId = request.OriginalPurchaseReceiptId.Value;
            pr.OriginalPurchaseReceipt = origReceipt;
        }

        if (request.Notes is not null)
            pr.Notes = request.Notes;

        // Replace lines if provided
        if (request.Lines is not null)
        {
            if (request.Lines.Count == 0)
                return (null, ErrorCodes.PurchaseReturnEmpty, "Purchase return must have at least one line.");

            var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
            var existingVariants = await _db.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync(ct);
            var missing = variantIds.Except(existingVariants).ToList();
            if (missing.Count > 0)
                return (null, ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

            // Validate reason codes
            var reasonCodeIds = request.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
            var activeReasonCodes = await _db.ReasonCodes
                .Where(rc => reasonCodeIds.Contains(rc.Id))
                .ToListAsync(ct);
            var missingReasons = reasonCodeIds.Except(activeReasonCodes.Select(rc => rc.Id)).ToList();
            if (missingReasons.Count > 0)
                return (null, ErrorCodes.ReasonCodeNotFound, $"Reason code(s) not found: {string.Join(", ", missingReasons)}");
            var inactiveReasons = activeReasonCodes.Where(rc => !rc.IsActive).ToList();
            if (inactiveReasons.Count > 0)
                return (null, ErrorCodes.ReasonCodeInactive, $"Reason code(s) inactive: {string.Join(", ", inactiveReasons.Select(r => r.Code))}");

            foreach (var line in request.Lines)
            {
                if (line.Quantity <= 0)
                    return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
                if (line.UnitCost < 0)
                    return (null, ErrorCodes.ValidationFailed, "Line unit cost cannot be negative.");
            }

            // Validate return qty vs received qty if original receipt
            var effectiveReceiptId = request.ClearOriginalReceipt
                ? null
                : (request.OriginalPurchaseReceiptId ?? pr.OriginalPurchaseReceiptId);
            if (effectiveReceiptId.HasValue)
            {
                var validationError = await ValidateReturnQtyAsync(
                    effectiveReceiptId.Value, request.Lines, pr.Id, ct);
                if (validationError is not null)
                    return (null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);
            }

            // Delete existing lines
            await _db.PurchaseReturnLines
                .Where(l => l.PurchaseReturnId == pr.Id)
                .ExecuteDeleteAsync(ct);

            decimal totalAmount = 0m;
            foreach (var line in request.Lines)
            {
                var lineTotal = line.Quantity * line.UnitCost;
                _db.PurchaseReturnLines.Add(new PurchaseReturnLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseReturnId = pr.Id,
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    LineTotal = lineTotal,
                    ReasonCodeId = line.ReasonCodeId,
                    DispositionType = line.DispositionType,
                    Notes = line.Notes,
                });
                totalAmount += lineTotal;
            }
            pr.TotalAmount = totalAmount;
        }

        await _db.SaveChangesAsync(ct);

        // Reload for DTO mapping
        var result = await GetByIdAsync(id, ct);
        return (result, null, null);
    }

    // ───── Delete (Draft only) ─────

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> DeleteAsync(
        Guid id, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseReturns.FindAsync([id], ct);
        if (pr is null)
            return (false, ErrorCodes.PurchaseReturnNotFound, "Purchase return not found.");

        if (pr.Status != PurchaseReturnStatus.Draft)
            return (false, ErrorCodes.PurchaseReturnAlreadyPosted, "Cannot delete a posted or voided purchase return.");

        _db.PurchaseReturns.Remove(pr);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ───── Post (Atomic + Idempotent) ─────

    /// <summary>
    /// Posts a draft purchase return atomically:
    /// 1. Claims the entity with an atomic UPDATE … WHERE Status = Draft (TOCTOU prevention)
    /// 2. Inside a single Serializable transaction:
    ///    a. Validates reason codes and return qty against original receipt
    ///    b. Creates a PurchaseReturnIssue stock movement (negative deltas = stock out)
    ///    c. Links the movement back to the return
    /// 3. Creates a DebitNote ledger entry (reduces supplier payable)
    /// Idempotent: if already posted, returns the existing movement ID.
    /// Concurrent duplicates get 409 POST_CONCURRENCY_CONFLICT.
    /// </summary>
    public async Task<PostResult> PostAsync(Guid returnId, Guid userId, CancellationToken ct = default)
    {
        // ── Atomic concurrency gate ──
        var claimed = await _db.PurchaseReturns
            .Where(pr => pr.Id == returnId && pr.Status == PurchaseReturnStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(pr => pr.Status, PurchaseReturnStatus.Posted)
                .SetProperty(pr => pr.PostedAtUtc, DateTime.UtcNow)
                .SetProperty(pr => pr.PostedByUserId, userId), ct);

        if (claimed == 0)
        {
            var existing = await _db.PurchaseReturns
                .AsNoTracking()
                .Where(pr => pr.Id == returnId)
                .Select(pr => new { pr.Status, pr.StockMovementId })
                .FirstOrDefaultAsync(ct);

            if (existing is null)
                return new PostResult(false, null, ErrorCodes.PurchaseReturnNotFound, "Purchase return not found.");

            if (existing.Status == PurchaseReturnStatus.Posted && existing.StockMovementId.HasValue)
                return new PostResult(true, existing.StockMovementId, ErrorCodes.PostAlreadyPosted, "Already posted.");

            if (existing.Status == PurchaseReturnStatus.Voided)
                return new PostResult(false, null, ErrorCodes.PurchaseReturnAlreadyVoided, "Purchase return has been voided.");

            return new PostResult(false, null, ErrorCodes.PostConcurrencyConflict,
                "Posting in progress by another request. Retry shortly.");
        }

        // ── Single transaction for validation + stock + linking ──
        var strategy = _db.Database.CreateExecutionStrategy();
        PostResult? postResult = null;
        Guid? supplierId = null;
        Guid? originalReceiptId = null;
        decimal totalAmount = 0;
        string? returnNumber = null;

        try
        {
            postResult = await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, ct);

                var purchaseReturn = await _db.PurchaseReturns
                    .Include(pr => pr.Lines)
                    .FirstOrDefaultAsync(pr => pr.Id == returnId, ct);

                if (purchaseReturn!.Lines.Count == 0)
                {
                    return new PostResult(false, null, ErrorCodes.PurchaseReturnEmpty, "Purchase return has no lines.");
                }

                // Capture values for post-transaction accounting
                supplierId = purchaseReturn.SupplierId;
                originalReceiptId = purchaseReturn.OriginalPurchaseReceiptId;
                totalAmount = purchaseReturn.TotalAmount;
                returnNumber = purchaseReturn.ReturnNumber;

                // Validate reason codes are still active at posting time
                var reasonCodeIds = purchaseReturn.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
                var reasonCodes = await _db.ReasonCodes
                    .Where(rc => reasonCodeIds.Contains(rc.Id))
                    .ToDictionaryAsync(rc => rc.Id, ct);
                foreach (var line in purchaseReturn.Lines)
                {
                    if (!reasonCodes.TryGetValue(line.ReasonCodeId, out var rc) || !rc.IsActive)
                    {
                        return new PostResult(false, null, ErrorCodes.ReasonCodeInactive,
                            $"Reason code '{rc?.Code ?? line.ReasonCodeId.ToString()}' is inactive.");
                    }
                }

                // Validate return qty if original receipt linked
                if (purchaseReturn.OriginalPurchaseReceiptId.HasValue)
                {
                    var lineRequests = purchaseReturn.Lines.Select(l =>
                        new ReturnLineRequest(l.VariantId, l.Quantity, l.UnitCost, l.ReasonCodeId, l.DispositionType, l.Notes))
                        .ToList();
                    var validationError = await ValidateReturnQtyAsync(
                        purchaseReturn.OriginalPurchaseReceiptId.Value, lineRequests, purchaseReturn.Id, ct);
                    if (validationError is not null)
                    {
                        return new PostResult(false, null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);
                    }
                }

                // Create PurchaseReturnIssue stock movement (negative delta = stock out)
                var stockLines = new List<StockService.PostMovementLineRequest>();
                foreach (var line in purchaseReturn.Lines)
                {
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId,
                        purchaseReturn.WarehouseId,
                        -line.Quantity, // Negative = stock out
                        line.UnitCost,
                        $"Purchase return {purchaseReturn.ReturnNumber} [{line.DispositionType}]"
                    ));
                }

                if (stockLines.Count > 0)
                {
                    var movementRequest = new StockService.PostMovementRequest(
                        MovementType.PurchaseReturnIssue,
                        purchaseReturn.ReturnNumber,
                        $"Purchase return {purchaseReturn.ReturnNumber}",
                        stockLines);

                    // StockService detects the ambient transaction — no nested tx
                    var stockResult = await _stockService.PostAsync(movementRequest, userId, ct);
                    if (!stockResult.Success)
                    {
                        return new PostResult(false, null, stockResult.ErrorCode, stockResult.ErrorDetail);
                    }

                    purchaseReturn.StockMovementId = stockResult.MovementId;
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new PostResult(true, purchaseReturn.StockMovementId, null, null);
            });
        }
        catch
        {
            await RollbackReturnClaimAsync(returnId, ct);
            throw;
        }

        if (postResult is not null && !postResult.Success)
        {
            await RollbackReturnClaimAsync(returnId, ct);
            return postResult;
        }

        // ── Create DebitNote ledger entry (separate concern, outside stock transaction) ──
        if (supplierId.HasValue && returnNumber is not null)
        {
            var entry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                PartyType = PartyType.Supplier,
                PartyId = supplierId.Value,
                EntryType = LedgerEntryType.DebitNote,
                Amount = -totalAmount,
                Reference = $"Debit note from purchase return {returnNumber}",
                RelatedInvoiceId = originalReceiptId,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId,
            };
            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
        }

        return postResult!;
    }

    // ───── Void (Draft only) ─────

    /// <summary>
    /// Voids a draft purchase return. Posted returns CANNOT be voided.
    /// Void is a soft-cancel that preserves the record with a terminal Voided status.
    /// </summary>
    public async Task<PostResult> VoidAsync(Guid returnId, Guid userId, CancellationToken ct = default)
    {
        var pr = await _db.PurchaseReturns
            .FirstOrDefaultAsync(r => r.Id == returnId, ct);

        if (pr is null)
            return new PostResult(false, null, ErrorCodes.PurchaseReturnNotFound, "Purchase return not found.");

        if (pr.Status == PurchaseReturnStatus.Voided)
            return new PostResult(false, null, ErrorCodes.PurchaseReturnAlreadyVoided, "Purchase return already voided.");

        if (pr.Status == PurchaseReturnStatus.Posted)
            return new PostResult(false, null, ErrorCodes.PurchaseReturnVoidNotAllowedAfterPost,
                "Cannot void a posted purchase return. Posted returns are permanent.");

        // Only Draft can be voided
        pr.Status = PurchaseReturnStatus.Voided;
        pr.VoidedAtUtc = DateTime.UtcNow;
        pr.VoidedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        return new PostResult(true, null, null, null);
    }

    // ───── Validation helpers ─────

    /// <summary>
    /// Validates that returned qty per variant does not exceed received qty
    /// minus already-returned qty from other posted returns linked to the same receipt.
    /// </summary>
    private async Task<(string ErrorCode, string ErrorDetail)?> ValidateReturnQtyAsync(
        Guid originalReceiptId, List<ReturnLineRequest> lines, Guid? excludeReturnId,
        CancellationToken ct)
    {
        // Get received quantities per variant from the original receipt
        var receiptLines = await _db.PurchaseReceiptLines
            .Where(l => l.PurchaseReceiptId == originalReceiptId)
            .Select(l => new { l.VariantId, l.Quantity })
            .ToListAsync(ct);

        var receivedQtys = receiptLines
            .GroupBy(l => l.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        // Get already-returned quantities per variant from OTHER posted returns on this receipt
        var alreadyReturnedQuery = _db.PurchaseReturnLines
            .Where(l => l.PurchaseReturn.OriginalPurchaseReceiptId == originalReceiptId
                && l.PurchaseReturn.Status == PurchaseReturnStatus.Posted);

        if (excludeReturnId.HasValue)
            alreadyReturnedQuery = alreadyReturnedQuery.Where(l => l.PurchaseReturnId != excludeReturnId.Value);

        var returnedLines = await alreadyReturnedQuery
            .Select(l => new { l.VariantId, l.Quantity })
            .ToListAsync(ct);

        var alreadyReturned = returnedLines
            .GroupBy(l => l.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        // Aggregate requested return qty per variant
        var requestedByVariant = lines
            .GroupBy(l => l.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        foreach (var (variantId, requestedQty) in requestedByVariant)
        {
            if (!receivedQtys.TryGetValue(variantId, out var received))
                return (ErrorCodes.ReturnQtyExceedsReceived,
                    $"Variant {variantId} was not received on the original purchase receipt.");

            var previouslyReturned = alreadyReturned.GetValueOrDefault(variantId, 0m);
            var available = received - previouslyReturned;

            if (requestedQty > available)
                return (ErrorCodes.ReturnQtyExceedsReceived,
                    $"Variant {variantId}: requested return qty {requestedQty} exceeds available {available} (received {received}, already returned {previouslyReturned}).");
        }

        return null;
    }

    /// <summary>Reverts the atomic claim if stock posting fails.</summary>
    private async Task RollbackReturnClaimAsync(Guid returnId, CancellationToken ct)
    {
        await _db.PurchaseReturns
            .Where(pr => pr.Id == returnId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(pr => pr.Status, PurchaseReturnStatus.Draft)
                .SetProperty(pr => pr.PostedAtUtc, (DateTime?)null)
                .SetProperty(pr => pr.PostedByUserId, (Guid?)null), ct);
    }

    private async Task EnsureReturnSequenceAsync(CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE SEQUENCE IF NOT EXISTS purchase_return_number_seq START WITH 1 INCREMENT BY 1;", ct);
    }

    // ───── Helpers ─────

    private async Task<Dictionary<Guid, ProductVariant>> LoadVariantDictAsync(
        List<Guid> variantIds, CancellationToken ct)
    {
        if (variantIds.Count == 0)
            return new Dictionary<Guid, ProductVariant>();

        return await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Id, ct);
    }

    private async Task<Dictionary<Guid, ReasonCode>> LoadReasonCodeDictAsync(
        List<Guid> reasonIds, CancellationToken ct)
    {
        if (reasonIds.Count == 0)
            return new Dictionary<Guid, ReasonCode>();

        return await _db.ReasonCodes
            .Where(rc => reasonIds.Contains(rc.Id))
            .AsNoTracking()
            .ToDictionaryAsync(rc => rc.Id, ct);
    }

    private static ReturnDto MapToDto(
        PurchaseReturn pr, string supplierName, string? originalDocNumber,
        string warehouseName, string createdByUsername,
        Dictionary<Guid, ProductVariant> variants,
        Dictionary<Guid, ReasonCode> reasons)
    {
        var lineDtos = pr.Lines.Select(l =>
        {
            var variant = variants.GetValueOrDefault(l.VariantId);
            var reason = reasons.GetValueOrDefault(l.ReasonCodeId);
            return new ReturnLineDto(
                l.Id, l.VariantId,
                variant?.Sku ?? "UNKNOWN",
                variant?.Product?.Name,
                l.Quantity, l.UnitCost, l.LineTotal,
                l.ReasonCodeId,
                reason?.Code ?? "UNKNOWN",
                reason?.NameAr ?? "",
                l.DispositionType.ToString(),
                l.Notes);
        }).ToList();

        return new ReturnDto(
            pr.Id, pr.ReturnNumber,
            pr.ReturnDateUtc,
            pr.SupplierId, supplierName,
            pr.OriginalPurchaseReceiptId, originalDocNumber,
            pr.WarehouseId, warehouseName,
            pr.CreatedByUserId, createdByUsername,
            pr.Notes, pr.Status.ToString(),
            pr.StockMovementId,
            pr.TotalAmount,
            pr.CreatedAtUtc, pr.PostedAtUtc,
            pr.PostedByUserId,
            lineDtos);
    }
}

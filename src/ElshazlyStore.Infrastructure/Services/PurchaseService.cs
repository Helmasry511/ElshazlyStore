using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles purchase receipt CRUD and idempotent posting.
/// Posting a receipt creates a StockMovement of type PurchaseReceipt
/// and a SupplierPayable placeholder record.
/// </summary>
public sealed class PurchaseService
{
    private readonly AppDbContext _db;
    private readonly StockService _stockService;
    private readonly AccountingService _accountingService;

    public PurchaseService(AppDbContext db, StockService stockService, AccountingService accountingService)
    {
        _db = db;
        _stockService = stockService;
        _accountingService = accountingService;
    }

    // ───── DTOs ─────

    public sealed record CreateReceiptRequest(
        Guid SupplierId,
        Guid WarehouseId,
        string? DocumentNumber,
        string? Notes,
        List<CreateReceiptLineRequest> Lines);

    public sealed record CreateReceiptLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal UnitCost);

    public sealed record UpdateReceiptRequest(
        Guid? SupplierId,
        Guid? WarehouseId,
        string? Notes,
        List<CreateReceiptLineRequest>? Lines);

    public sealed record ReceiptDto(
        Guid Id, string DocumentNumber, Guid SupplierId, string SupplierName,
        Guid WarehouseId, string WarehouseName, string? Notes,
        string Status, Guid? StockMovementId,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        Guid CreatedByUserId, string CreatedByUsername,
        List<ReceiptLineDto> Lines);

    public sealed record ReceiptLineDto(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal UnitCost, decimal LineTotal);

    public sealed record PostResult(bool Success, Guid? StockMovementId, string? ErrorCode, string? ErrorDetail);

    // ───── Create ─────

    public async Task<(ReceiptDto? Receipt, string? ErrorCode, string? ErrorDetail)> CreateAsync(
        CreateReceiptRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Lines is not { Count: > 0 })
            return (null, ErrorCodes.PurchaseReceiptEmpty, "Purchase receipt must have at least one line.");

        // Validate supplier
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId && s.IsActive, ct);
        if (supplier is null)
            return (null, ErrorCodes.SupplierNotFound, "Supplier not found or inactive.");

        // Validate warehouse
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive, ct);
        if (warehouse is null)
            return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");

        // Validate variants
        var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var existingVariants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var missing = variantIds.Except(existingVariants).ToList();
        if (missing.Count > 0)
            return (null, ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

        // Validate positive quantities and costs
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
            if (line.UnitCost < 0)
                return (null, ErrorCodes.ValidationFailed, "Unit cost cannot be negative.");
        }

        // Generate document number if not provided
        var docNumber = request.DocumentNumber;
        if (string.IsNullOrWhiteSpace(docNumber))
        {
            if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                var seq = await _db.Database
                    .SqlQueryRaw<long>("SELECT nextval('purchase_receipt_number_seq') AS \"Value\"")
                    .SingleAsync(ct);
                docNumber = $"PR-{seq:D6}";
            }
            else
            {
                // Fallback for SQLite (tests)
                var existingCount = await _db.PurchaseReceipts
                    .Where(r => r.DocumentNumber.StartsWith("PR-")
                        && r.DocumentNumber.Length == 9)
                    .CountAsync(ct);
                docNumber = $"PR-{existingCount + 1:D6}";
                while (await _db.PurchaseReceipts.AnyAsync(r => r.DocumentNumber == docNumber, ct))
                {
                    existingCount++;
                    docNumber = $"PR-{existingCount + 1:D6}";
                }
            }
        }
        else
        {
            if (await _db.PurchaseReceipts.AnyAsync(r => r.DocumentNumber == docNumber, ct))
                return (null, ErrorCodes.DocumentNumberExists, $"Document number '{docNumber}' already exists.");
        }

        var receipt = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            DocumentNumber = docNumber,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            Notes = request.Notes,
            Status = PurchaseReceiptStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        foreach (var line in request.Lines)
        {
            receipt.Lines.Add(new PurchaseReceiptLine
            {
                Id = Guid.NewGuid(),
                PurchaseReceiptId = receipt.Id,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
            });
        }

        _db.PurchaseReceipts.Add(receipt);
        await _db.SaveChangesAsync(ct);

        // Load navigations for DTO
        var user = await _db.Users.FindAsync([userId], ct);
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var dto = MapToDto(receipt, supplier.Name, warehouse.Name, user!.Username, variants);
        return (dto, null, null);
    }

    // ───── Get ─────

    public async Task<ReceiptDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var receipt = await _db.PurchaseReceipts
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (receipt is null) return null;

        var variantIds = receipt.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        return MapToDto(receipt, receipt.Supplier.Name, receipt.Warehouse.Name,
            receipt.CreatedBy.Username, variants);
    }

    // ───── List / Search ─────

    public async Task<(List<ReceiptDto> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize, string? sort,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.PurchaseReceipts
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .AsNoTracking()
            .AsQueryable();

        query = query.ApplySearch(_db.Database, search,
            r => r.DocumentNumber,
            r => r.Supplier.Name,
            r => r.Notes);

        query = sort?.ToLowerInvariant() switch
        {
            "document" => query.OrderBy(r => r.DocumentNumber),
            "document_desc" => query.OrderByDescending(r => r.DocumentNumber),
            "supplier" => query.OrderBy(r => r.Supplier.Name),
            "supplier_desc" => query.OrderByDescending(r => r.Supplier.Name),
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

        // Fetch variants for all lines
        var allVariantIds = items.SelectMany(r => r.Lines).Select(l => l.VariantId).Distinct().ToList();
        var allVariants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => allVariantIds.Contains(v.Id))
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Id, ct);

        var dtos = items.Select(r => MapToDto(r, r.Supplier.Name, r.Warehouse.Name,
            r.CreatedBy.Username, allVariants)).ToList();

        return (dtos, totalCount);
    }

    // ───── Update (Draft only) ─────

    public async Task<(ReceiptDto? Receipt, string? ErrorCode, string? ErrorDetail)> UpdateAsync(
        Guid id, UpdateReceiptRequest request, CancellationToken ct = default)
    {
        var receipt = await _db.PurchaseReceipts
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (receipt is null)
            return (null, ErrorCodes.PurchaseReceiptNotFound, "Purchase receipt not found.");

        if (receipt.Status != PurchaseReceiptStatus.Draft)
            return (null, ErrorCodes.PurchaseReceiptAlreadyPosted, "Cannot modify a posted purchase receipt.");

        if (request.SupplierId.HasValue)
        {
            var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId.Value && s.IsActive, ct);
            if (supplier is null)
                return (null, ErrorCodes.SupplierNotFound, "Supplier not found or inactive.");
            receipt.SupplierId = request.SupplierId.Value;
            receipt.Supplier = supplier;
        }

        if (request.WarehouseId.HasValue)
        {
            var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value && w.IsActive, ct);
            if (warehouse is null)
                return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");
            receipt.WarehouseId = request.WarehouseId.Value;
            receipt.Warehouse = warehouse;
        }

        if (request.Notes is not null)
            receipt.Notes = request.Notes;

        if (request.Lines is not null)
        {
            if (request.Lines.Count == 0)
                return (null, ErrorCodes.PurchaseReceiptEmpty, "Purchase receipt must have at least one line.");

            // Validate variants
            var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
            var existingVariants = await _db.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync(ct);
            var missing = variantIds.Except(existingVariants).ToList();
            if (missing.Count > 0)
                return (null, ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

            foreach (var line in request.Lines)
            {
                if (line.Quantity <= 0)
                    return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
                if (line.UnitCost < 0)
                    return (null, ErrorCodes.ValidationFailed, "Unit cost cannot be negative.");
            }

            // Replace lines
            _db.PurchaseReceiptLines.RemoveRange(receipt.Lines);
            receipt.Lines.Clear();

            foreach (var line in request.Lines)
            {
                receipt.Lines.Add(new PurchaseReceiptLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseReceiptId = receipt.Id,
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Reload navigations for DTO
        var allVariantIds = receipt.Lines.Select(l => l.VariantId).Distinct().ToList();
        var allVariants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => allVariantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var dto = MapToDto(receipt, receipt.Supplier.Name, receipt.Warehouse.Name,
            receipt.CreatedBy.Username, allVariants);
        return (dto, null, null);
    }

    // ───── Delete (Draft only) ─────

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> DeleteAsync(
        Guid id, CancellationToken ct = default)
    {
        var receipt = await _db.PurchaseReceipts.FindAsync([id], ct);
        if (receipt is null)
            return (false, ErrorCodes.PurchaseReceiptNotFound, "Purchase receipt not found.");

        if (receipt.Status != PurchaseReceiptStatus.Draft)
            return (false, ErrorCodes.PurchaseReceiptAlreadyPosted, "Cannot delete a posted purchase receipt.");

        _db.PurchaseReceipts.Remove(receipt);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ───── Post (Atomic + Idempotent) ─────

    /// <summary>
    /// Posts a draft purchase receipt atomically:
    /// 1. Claims the entity with an atomic UPDATE … WHERE Status = Draft (TOCTOU prevention)
    /// 2. Inside a single Serializable transaction:
    ///    a. Creates a StockMovement of type PurchaseReceipt (balance + ledger in one save)
    ///    b. Links the movement back to the receipt
    ///    c. Creates a SupplierPayable placeholder
    /// 3. Creates an AP ledger entry
    /// Idempotent: if already posted, returns the existing StockMovementId.
    /// Concurrent duplicates get 409 POST_CONCURRENCY_CONFLICT.
    /// </summary>
    public async Task<PostResult> PostAsync(Guid receiptId, Guid userId, CancellationToken ct = default)
    {
        // ── Atomic concurrency gate (auto-commits, outside transaction) ──
        var claimed = await _db.PurchaseReceipts
            .Where(r => r.Id == receiptId && r.Status == PurchaseReceiptStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, PurchaseReceiptStatus.Posted)
                .SetProperty(r => r.PostedAtUtc, DateTime.UtcNow), ct);

        if (claimed == 0)
        {
            var existing = await _db.PurchaseReceipts
                .AsNoTracking()
                .Where(r => r.Id == receiptId)
                .Select(r => new { r.Status, r.StockMovementId })
                .FirstOrDefaultAsync(ct);

            if (existing is null)
                return new PostResult(false, null, ErrorCodes.PurchaseReceiptNotFound, "Purchase receipt not found.");

            if (existing.Status == PurchaseReceiptStatus.Posted && existing.StockMovementId.HasValue)
                return new PostResult(true, existing.StockMovementId, ErrorCodes.PostAlreadyPosted, "Already posted.");

            return new PostResult(false, null, ErrorCodes.PostConcurrencyConflict,
                "Posting in progress by another request. Retry shortly.");
        }

        // ── Single transaction for stock + linking + payable ──
        var strategy = _db.Database.CreateExecutionStrategy();
        PostResult? postResult = null;
        try
        {
            postResult = await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, ct);

                var receipt = await _db.PurchaseReceipts
                    .Include(r => r.Lines)
                    .FirstOrDefaultAsync(r => r.Id == receiptId, ct);

                if (receipt!.Lines.Count == 0)
                {
                    return new PostResult(false, null, ErrorCodes.PurchaseReceiptEmpty, "Purchase receipt has no lines.");
                }

                // Create stock movement (positive deltas = stock in)
                var movementLines = receipt.Lines.Select(line =>
                    new StockService.PostMovementLineRequest(
                        line.VariantId,
                        receipt.WarehouseId,
                        line.Quantity,
                        line.UnitCost,
                        $"Purchase receipt {receipt.DocumentNumber}"
                    )).ToList();

                var movementRequest = new StockService.PostMovementRequest(
                    MovementType.PurchaseReceipt,
                    receipt.DocumentNumber,
                    receipt.Notes,
                    movementLines);

                // StockService detects the ambient transaction — no nested tx
                var movementResult = await _stockService.PostAsync(movementRequest, userId, ct);
                if (!movementResult.Success)
                {
                    return new PostResult(false, null, movementResult.ErrorCode, movementResult.ErrorDetail);
                }

                // Link movement + create payable inside the SAME transaction
                receipt.StockMovementId = movementResult.MovementId;

                var totalAmount = receipt.Lines.Sum(l => l.Quantity * l.UnitCost);
                _db.SupplierPayables.Add(new SupplierPayable
                {
                    Id = Guid.NewGuid(),
                    SupplierId = receipt.SupplierId,
                    PurchaseReceiptId = receipt.Id,
                    Amount = totalAmount,
                    IsPaid = false,
                    CreatedAtUtc = DateTime.UtcNow,
                });

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new PostResult(true, movementResult.MovementId, null, null);
            });
        }
        catch
        {
            await RollbackPurchaseClaimAsync(receiptId, ct);
            throw;
        }

        if (postResult is not null && !postResult.Success)
        {
            await RollbackPurchaseClaimAsync(receiptId, ct);
            return postResult;
        }

        // Create AP ledger entry (separate concern, outside the stock transaction)
        var postedReceipt = await _db.PurchaseReceipts
            .Include(r => r.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct);

        if (postedReceipt is not null)
        {
            var totalAmt = postedReceipt.Lines.Sum(l => l.Quantity * l.UnitCost);
            await _accountingService.CreateInvoiceEntryAsync(
                PartyType.Supplier, postedReceipt.SupplierId,
                totalAmt, postedReceipt.Id,
                $"Purchase receipt {postedReceipt.DocumentNumber}",
                userId, ct);
        }

        return postResult!;
    }

    /// <summary>Reverts the atomic claim if stock posting fails.</summary>
    private async Task RollbackPurchaseClaimAsync(Guid receiptId, CancellationToken ct)
    {
        await _db.PurchaseReceipts
            .Where(r => r.Id == receiptId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, PurchaseReceiptStatus.Draft)
                .SetProperty(r => r.PostedAtUtc, (DateTime?)null), ct);
    }

    // ───── Mapping ─────

    private static ReceiptDto MapToDto(
        PurchaseReceipt receipt, string supplierName, string warehouseName,
        string createdByUsername, Dictionary<Guid, ProductVariant> variants)
    {
        var lineDtos = receipt.Lines.Select(l =>
        {
            var variant = variants.GetValueOrDefault(l.VariantId);
            return new ReceiptLineDto(
                l.Id, l.VariantId,
                variant?.Sku ?? "UNKNOWN",
                variant?.Product?.Name,
                l.Quantity, l.UnitCost,
                l.Quantity * l.UnitCost);
        }).ToList();

        return new ReceiptDto(
            receipt.Id, receipt.DocumentNumber,
            receipt.SupplierId, supplierName,
            receipt.WarehouseId, warehouseName,
            receipt.Notes, receipt.Status.ToString(),
            receipt.StockMovementId,
            receipt.CreatedAtUtc, receipt.PostedAtUtc,
            receipt.CreatedByUserId, createdByUsername,
            lineDtos);
    }
}

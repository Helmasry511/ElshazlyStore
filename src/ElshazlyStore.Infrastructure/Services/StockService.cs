using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Transactional stock movement posting with concurrency-safe balance updates.
/// No endpoint may directly set stock quantities — all mutations go through PostAsync.
/// </summary>
public sealed class StockService
{
    private readonly AppDbContext _db;

    public StockService(AppDbContext db)
    {
        _db = db;
    }

    // ───── DTOs ─────

    public sealed record PostMovementRequest(
        MovementType Type,
        string? Reference,
        string? Notes,
        List<PostMovementLineRequest> Lines);

    public sealed record PostMovementLineRequest(
        Guid VariantId,
        Guid WarehouseId,
        decimal QuantityDelta,
        decimal? UnitCost,
        string? Reason);

    public sealed record PostMovementResult(bool Success, Guid? MovementId, string? ErrorCode, string? ErrorDetail);

    public sealed record BalanceRow(
        Guid VariantId, string Sku, string? Color, string? Size,
        string ProductName, string? Barcode,
        Guid WarehouseId, string WarehouseCode, string WarehouseName,
        decimal Quantity, DateTime LastUpdatedUtc);

    public sealed record LedgerRow(
        Guid MovementId, MovementType Type, string? Reference,
        DateTime PostedAtUtc, string PostedByUsername,
        Guid VariantId, string Sku,
        Guid WarehouseId, string WarehouseCode,
        decimal QuantityDelta, decimal? UnitCost, string? Reason);

    // ───── Post Movement ─────

    /// <summary>
    /// Posts a stock movement. If an ambient transaction exists on the DbContext,
    /// the work runs inside it (caller owns the transaction). Otherwise a fresh
    /// Serializable transaction is created with the retry-aware execution strategy.
    /// </summary>
    public async Task<PostMovementResult> PostAsync(PostMovementRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Lines is not { Count: > 0 })
            return new PostMovementResult(false, null, ErrorCodes.MovementEmpty, "Movement must have at least one line.");

        var validationError = ValidateLines(request);
        if (validationError is not null)
            return validationError;

        // If the caller already owns a transaction, run inside it directly.
        if (_db.Database.CurrentTransaction is not null)
            return await PostCoreAsync(request, userId, ct);

        // Otherwise create our own Serializable transaction with retry.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);

            try
            {
                var result = await PostCoreAsync(request, userId, ct);
                if (result.Success)
                    await tx.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                return new PostMovementResult(false, null, ErrorCodes.Conflict,
                    "Concurrent stock update detected. Please retry.");
            }
        });
    }

    /// <summary>Core posting logic — caller must guarantee an ambient transaction.</summary>
    private async Task<PostMovementResult> PostCoreAsync(
        PostMovementRequest request, Guid userId, CancellationToken ct)
    {
        // Validate all variant IDs exist
        var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var existingVariants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var missingVariants = variantIds.Except(existingVariants).ToList();
        if (missingVariants.Count > 0)
            return new PostMovementResult(false, null, ErrorCodes.VariantNotFound,
                $"Variant(s) not found: {string.Join(", ", missingVariants)}");

        // Validate all warehouse IDs exist
        var warehouseIds = request.Lines.Select(l => l.WarehouseId).Distinct().ToList();
        var existingWarehouses = await _db.Warehouses
            .Where(w => warehouseIds.Contains(w.Id) && w.IsActive)
            .Select(w => w.Id)
            .ToListAsync(ct);
        var missingWarehouses = warehouseIds.Except(existingWarehouses).ToList();
        if (missingWarehouses.Count > 0)
            return new PostMovementResult(false, null, ErrorCodes.WarehouseNotFound,
                $"Warehouse(s) not found or inactive: {string.Join(", ", missingWarehouses)}");

        // Build the movement header
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Reference = request.Reference,
            Notes = request.Notes,
            PostedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        // Build lines
        foreach (var line in request.Lines)
        {
            movement.Lines.Add(new StockMovementLine
            {
                Id = Guid.NewGuid(),
                StockMovementId = movement.Id,
                VariantId = line.VariantId,
                WarehouseId = line.WarehouseId,
                QuantityDelta = line.QuantityDelta,
                UnitCost = line.UnitCost,
                Reason = line.Reason,
            });
        }

        _db.StockMovements.Add(movement);

        // Update balances — group lines by (variant, warehouse) and aggregate
        var grouped = request.Lines
            .GroupBy(l => (l.VariantId, l.WarehouseId))
            .Select(g => new { g.Key.VariantId, g.Key.WarehouseId, TotalDelta = g.Sum(l => l.QuantityDelta) })
            .ToList();

        foreach (var g in grouped)
        {
            var balance = await _db.StockBalances
                .FirstOrDefaultAsync(b => b.VariantId == g.VariantId && b.WarehouseId == g.WarehouseId, ct);

            if (balance is null)
            {
                if (g.TotalDelta < 0)
                {
                    return new PostMovementResult(false, null, ErrorCodes.StockNegativeNotAllowed,
                        $"Insufficient stock for variant {g.VariantId} in warehouse {g.WarehouseId}. " +
                        $"Available: 0, requested delta: {g.TotalDelta}");
                }

                balance = new StockBalance
                {
                    Id = Guid.NewGuid(),
                    VariantId = g.VariantId,
                    WarehouseId = g.WarehouseId,
                    Quantity = g.TotalDelta,
                    LastUpdatedUtc = DateTime.UtcNow,
                };
                _db.StockBalances.Add(balance);
            }
            else
            {
                var newQty = balance.Quantity + g.TotalDelta;
                if (newQty < 0)
                {
                    return new PostMovementResult(false, null, ErrorCodes.StockNegativeNotAllowed,
                        $"Insufficient stock for variant {g.VariantId} in warehouse {g.WarehouseId}. " +
                        $"Available: {balance.Quantity}, requested delta: {g.TotalDelta}");
                }

                balance.Quantity = newQty;
                balance.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new PostMovementResult(true, movement.Id, null, null);
    }

    // ───── Queries ─────

    /// <summary>
    /// Returns balances where the Quantity is the authoritative movement-derived
    /// value (SUM of QuantityDelta) rather than the cached StockBalance column.
    /// The StockBalance table is used for filtering / sorting / pagination,
    /// but quantities are re-computed from movements in a second query so
    /// balances can never diverge from the ledger.
    /// </summary>
    public async Task<(List<BalanceRow> Items, int TotalCount)> GetBalancesAsync(
        Guid? warehouseId, string? search, int page, int pageSize, string sort,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.StockBalances
            .Include(b => b.Variant).ThenInclude(v => v.Product)
            .Include(b => b.Variant).ThenInclude(v => v.BarcodeReservation)
            .Include(b => b.Warehouse)
            .AsNoTracking()
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == warehouseId.Value);

        query = query.ApplySearch(_db.Database, search,
            b => b.Variant.Sku,
            b => b.Variant.Product.Name,
            b => b.Variant.BarcodeReservation!.Barcode,
            b => b.Variant.Color);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;

        query = sort?.ToLower() switch
        {
            "sku" => query.OrderBy(b => b.Variant.Sku),
            "sku_desc" => query.OrderByDescending(b => b.Variant.Sku),
            "quantity" => query.OrderBy(b => b.Quantity),
            "quantity_desc" => query.OrderByDescending(b => b.Quantity),
            "product" => query.OrderBy(b => b.Variant.Product.Name),
            "updated" => query.OrderBy(b => b.LastUpdatedUtc),
            "updated_desc" => query.OrderByDescending(b => b.LastUpdatedUtc),
            _ => query.OrderBy(b => b.Variant.Product.Name).ThenBy(b => b.Variant.Sku),
        };

        // Step 1: Fetch the paginated balance rows (structure / display data).
        var pagedRows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.VariantId,
                Sku = b.Variant.Sku,
                Color = b.Variant.Color,
                Size = b.Variant.Size,
                ProductName = b.Variant.Product.Name,
                Barcode = b.Variant.BarcodeReservation != null
                    ? b.Variant.BarcodeReservation.Barcode : null,
                b.WarehouseId,
                WarehouseCode = b.Warehouse.Code,
                WarehouseName = b.Warehouse.Name,
                b.LastUpdatedUtc,
            })
            .ToListAsync(ct);

        if (pagedRows.Count == 0)
            return (new List<BalanceRow>(), totalCount);

        // Step 2: Compute authoritative quantities from movements for this page.
        var variantIds = pagedRows.Select(r => r.VariantId).Distinct().ToList();
        var whIds = pagedRows.Select(r => r.WarehouseId).Distinct().ToList();

        var movementLines = await _db.StockMovementLines
            .Where(l => variantIds.Contains(l.VariantId) && whIds.Contains(l.WarehouseId))
            .Select(l => new { l.VariantId, l.WarehouseId, l.QuantityDelta })
            .ToListAsync(ct);

        var qtyLookup = movementLines
            .GroupBy(l => (l.VariantId, l.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.QuantityDelta));

        // Step 3: Merge — use movement-derived quantity.
        var items = pagedRows.Select(r => new BalanceRow(
            r.VariantId, r.Sku, r.Color, r.Size,
            r.ProductName, r.Barcode,
            r.WarehouseId, r.WarehouseCode, r.WarehouseName,
            qtyLookup.GetValueOrDefault((r.VariantId, r.WarehouseId)),
            r.LastUpdatedUtc)).ToList();

        return (items, totalCount);
    }

    public async Task<(List<LedgerRow> Items, int TotalCount)> GetLedgerAsync(
        Guid? variantId, Guid? warehouseId, DateTime? from, DateTime? to,
        int page, int pageSize, bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.StockMovementLines
            .Include(l => l.StockMovement).ThenInclude(m => m.CreatedBy)
            .Include(l => l.Variant)
            .Include(l => l.Warehouse)
            .AsNoTracking()
            .AsQueryable();

        if (variantId.HasValue)
            query = query.Where(l => l.VariantId == variantId.Value);
        if (warehouseId.HasValue)
            query = query.Where(l => l.WarehouseId == warehouseId.Value);
        if (from.HasValue)
            query = query.Where(l => l.StockMovement.PostedAtUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.StockMovement.PostedAtUtc <= to.Value);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;

        var items = await query
            .OrderByDescending(l => l.StockMovement.PostedAtUtc)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LedgerRow(
                l.StockMovementId, l.StockMovement.Type, l.StockMovement.Reference,
                l.StockMovement.PostedAtUtc, l.StockMovement.CreatedBy.Username,
                l.VariantId, l.Variant.Sku,
                l.WarehouseId, l.Warehouse.Code,
                l.QuantityDelta, l.UnitCost, l.Reason))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    // ───── Helpers ─────

    private static PostMovementResult? ValidateLines(PostMovementRequest request)
    {
        foreach (var line in request.Lines)
        {
            if (line.QuantityDelta == 0)
                return new PostMovementResult(false, null, ErrorCodes.ValidationFailed,
                    "QuantityDelta cannot be zero.");

            switch (request.Type)
            {
                case MovementType.PurchaseReceipt:
                case MovementType.OpeningBalance:
                case MovementType.ProductionProduce:
                    if (line.QuantityDelta < 0)
                        return new PostMovementResult(false, null, ErrorCodes.ValidationFailed,
                            $"{request.Type} lines must have positive QuantityDelta.");
                    break;

                case MovementType.SaleIssue:
                case MovementType.ProductionConsume:
                case MovementType.PurchaseReturnIssue:
                    if (line.QuantityDelta > 0)
                        return new PostMovementResult(false, null, ErrorCodes.ValidationFailed,
                            $"{request.Type} lines must have negative QuantityDelta.");
                    break;

                // Transfer and Adjustment can have any sign
            }
        }

        // ── Transfer invariant: no stock creation / destruction ──
        if (request.Type == MovementType.Transfer)
        {
            // (a) Total quantity across all lines must be zero
            var totalSum = request.Lines.Sum(l => l.QuantityDelta);
            if (totalSum != 0)
                return new PostMovementResult(false, null, ErrorCodes.TransferUnbalanced,
                    $"Transfer lines must net to zero. Current total: {totalSum}.");

            // (b) Per-variant: each variant must independently net to zero
            var perVariant = request.Lines
                .GroupBy(l => l.VariantId)
                .Where(g => g.Sum(l => l.QuantityDelta) != 0)
                .Select(g => g.Key)
                .ToList();
            if (perVariant.Count > 0)
                return new PostMovementResult(false, null, ErrorCodes.TransferUnbalanced,
                    $"Transfer is unbalanced for variant(s): {string.Join(", ", perVariant)}.");

            // (c) At least two distinct warehouses
            var distinctWarehouses = request.Lines.Select(l => l.WarehouseId).Distinct().Count();
            if (distinctWarehouses < 2)
                return new PostMovementResult(false, null, ErrorCodes.TransferUnbalanced,
                    "Transfer must involve at least two distinct warehouses.");
        }

        return null;
    }
}

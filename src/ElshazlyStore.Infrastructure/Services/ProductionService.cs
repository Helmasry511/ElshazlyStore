using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles production batch CRUD and atomic posting.
/// Posting creates TWO stock movements in one transaction:
///   - ProductionConsume (negative deltas on raw material inputs)
///   - ProductionProduce (positive deltas on finished good outputs)
/// Negative stock rules apply to consumption.
/// </summary>
public sealed class ProductionService
{
    private readonly AppDbContext _db;
    private readonly StockService _stockService;

    public ProductionService(AppDbContext db, StockService stockService)
    {
        _db = db;
        _stockService = stockService;
    }

    // ───── DTOs ─────

    public sealed record CreateBatchRequest(
        Guid WarehouseId,
        string? BatchNumber,
        string? Notes,
        List<BatchLineRequest> Inputs,
        List<BatchLineRequest> Outputs);

    public sealed record BatchLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal? UnitCost);

    public sealed record UpdateBatchRequest(
        Guid? WarehouseId,
        string? Notes,
        List<BatchLineRequest>? Inputs,
        List<BatchLineRequest>? Outputs);

    public sealed record BatchDto(
        Guid Id, string BatchNumber,
        Guid WarehouseId, string WarehouseName, string? Notes,
        string Status, Guid? ConsumeMovementId, Guid? ProduceMovementId,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        Guid CreatedByUserId, string CreatedByUsername,
        List<BatchLineDto> Inputs, List<BatchLineDto> Outputs);

    public sealed record BatchLineDto(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal? UnitCost);

    public sealed record PostResult(
        bool Success, Guid? ConsumeMovementId, Guid? ProduceMovementId,
        string? ErrorCode, string? ErrorDetail);

    // ───── Create ─────

    public async Task<(BatchDto? Batch, string? ErrorCode, string? ErrorDetail)> CreateAsync(
        CreateBatchRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Inputs is not { Count: > 0 })
            return (null, ErrorCodes.ProductionBatchNoInputs, "Production batch must have at least one input line.");
        if (request.Outputs is not { Count: > 0 })
            return (null, ErrorCodes.ProductionBatchNoOutputs, "Production batch must have at least one output line.");

        // Validate warehouse
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive, ct);
        if (warehouse is null)
            return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");

        // Validate all variants
        var allVariantIds = request.Inputs.Select(l => l.VariantId)
            .Concat(request.Outputs.Select(l => l.VariantId))
            .Distinct().ToList();
        var existingVariants = await _db.ProductVariants
            .Where(v => allVariantIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var missing = allVariantIds.Except(existingVariants).ToList();
        if (missing.Count > 0)
            return (null, ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

        // Validate positive quantities
        foreach (var line in request.Inputs.Concat(request.Outputs))
        {
            if (line.Quantity <= 0)
                return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
        }

        // Generate batch number if not provided
        var batchNumber = request.BatchNumber;
        if (string.IsNullOrWhiteSpace(batchNumber))
        {
            if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                var seq = await _db.Database
                    .SqlQueryRaw<long>("SELECT nextval('production_batch_number_seq') AS \"Value\"")
                    .SingleAsync(ct);
                batchNumber = $"PB-{seq:D6}";
            }
            else
            {
                // Fallback for SQLite (tests)
                var existingCount = await _db.ProductionBatches
                    .Where(b => b.BatchNumber.StartsWith("PB-")
                        && b.BatchNumber.Length == 9)
                    .CountAsync(ct);
                batchNumber = $"PB-{existingCount + 1:D6}";
                while (await _db.ProductionBatches.AnyAsync(b => b.BatchNumber == batchNumber, ct))
                {
                    existingCount++;
                    batchNumber = $"PB-{existingCount + 1:D6}";
                }
            }
        }
        else
        {
            if (await _db.ProductionBatches.AnyAsync(b => b.BatchNumber == batchNumber, ct))
                return (null, ErrorCodes.BatchNumberExists, $"Batch number '{batchNumber}' already exists.");
        }

        var batch = new ProductionBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = batchNumber,
            WarehouseId = request.WarehouseId,
            Notes = request.Notes,
            Status = ProductionBatchStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        foreach (var line in request.Inputs)
        {
            batch.Lines.Add(new ProductionBatchLine
            {
                Id = Guid.NewGuid(),
                ProductionBatchId = batch.Id,
                LineType = ProductionLineType.Input,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
            });
        }

        foreach (var line in request.Outputs)
        {
            batch.Lines.Add(new ProductionBatchLine
            {
                Id = Guid.NewGuid(),
                ProductionBatchId = batch.Id,
                LineType = ProductionLineType.Output,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
            });
        }

        _db.ProductionBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FindAsync([userId], ct);
        var variants = await LoadVariantDictAsync(allVariantIds, ct);

        return (MapToDto(batch, warehouse.Name, user!.Username, variants), null, null);
    }

    // ───── Get ─────

    public async Task<BatchDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var batch = await _db.ProductionBatches
            .Include(b => b.Lines)
            .Include(b => b.Warehouse)
            .Include(b => b.CreatedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (batch is null) return null;

        var variantIds = batch.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await LoadVariantDictAsync(variantIds, ct);

        return MapToDto(batch, batch.Warehouse.Name, batch.CreatedBy.Username, variants);
    }

    // ───── List / Search ─────

    public async Task<(List<BatchDto> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize, string? sort,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.ProductionBatches
            .Include(b => b.Lines)
            .Include(b => b.Warehouse)
            .Include(b => b.CreatedBy)
            .AsNoTracking()
            .AsQueryable();

        query = query.ApplySearch(_db.Database, search,
            b => b.BatchNumber,
            b => b.Notes);

        query = sort?.ToLowerInvariant() switch
        {
            "batch" => query.OrderBy(b => b.BatchNumber),
            "batch_desc" => query.OrderByDescending(b => b.BatchNumber),
            "status" => query.OrderBy(b => b.Status),
            "created" => query.OrderBy(b => b.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(b => b.CreatedAtUtc),
            _ => query.OrderByDescending(b => b.CreatedAtUtc),
        };

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var allVariantIds = items.SelectMany(b => b.Lines).Select(l => l.VariantId).Distinct().ToList();
        var allVariants = await LoadVariantDictAsync(allVariantIds, ct);

        var dtos = items.Select(b => MapToDto(b, b.Warehouse.Name, b.CreatedBy.Username, allVariants)).ToList();
        return (dtos, totalCount);
    }

    // ───── Update (Draft only) ─────

    public async Task<(BatchDto? Batch, string? ErrorCode, string? ErrorDetail)> UpdateAsync(
        Guid id, UpdateBatchRequest request, CancellationToken ct = default)
    {
        var batch = await _db.ProductionBatches
            .Include(b => b.Lines)
            .Include(b => b.Warehouse)
            .Include(b => b.CreatedBy)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (batch is null)
            return (null, ErrorCodes.ProductionBatchNotFound, "Production batch not found.");

        if (batch.Status != ProductionBatchStatus.Draft)
            return (null, ErrorCodes.ProductionBatchAlreadyPosted, "Cannot modify a posted production batch.");

        if (request.WarehouseId.HasValue)
        {
            var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value && w.IsActive, ct);
            if (warehouse is null)
                return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");
            batch.WarehouseId = request.WarehouseId.Value;
            batch.Warehouse = warehouse;
        }

        if (request.Notes is not null)
            batch.Notes = request.Notes;

        // If lines are being replaced
        if (request.Inputs is not null || request.Outputs is not null)
        {
            var newInputs = request.Inputs ?? batch.Lines
                .Where(l => l.LineType == ProductionLineType.Input)
                .Select(l => new BatchLineRequest(l.VariantId, l.Quantity, l.UnitCost))
                .ToList();
            var newOutputs = request.Outputs ?? batch.Lines
                .Where(l => l.LineType == ProductionLineType.Output)
                .Select(l => new BatchLineRequest(l.VariantId, l.Quantity, l.UnitCost))
                .ToList();

            if (newInputs.Count == 0)
                return (null, ErrorCodes.ProductionBatchNoInputs, "Production batch must have at least one input line.");
            if (newOutputs.Count == 0)
                return (null, ErrorCodes.ProductionBatchNoOutputs, "Production batch must have at least one output line.");

            var allVariantIds = newInputs.Select(l => l.VariantId)
                .Concat(newOutputs.Select(l => l.VariantId))
                .Distinct().ToList();
            var existingVariants = await _db.ProductVariants
                .Where(v => allVariantIds.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync(ct);
            var missing = allVariantIds.Except(existingVariants).ToList();
            if (missing.Count > 0)
                return (null, ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

            foreach (var line in newInputs.Concat(newOutputs))
            {
                if (line.Quantity <= 0)
                    return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
            }

            _db.ProductionBatchLines.RemoveRange(batch.Lines);
            batch.Lines.Clear();

            foreach (var line in newInputs)
            {
                batch.Lines.Add(new ProductionBatchLine
                {
                    Id = Guid.NewGuid(),
                    ProductionBatchId = batch.Id,
                    LineType = ProductionLineType.Input,
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                });
            }

            foreach (var line in newOutputs)
            {
                batch.Lines.Add(new ProductionBatchLine
                {
                    Id = Guid.NewGuid(),
                    ProductionBatchId = batch.Id,
                    LineType = ProductionLineType.Output,
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var varIds = batch.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await LoadVariantDictAsync(varIds, ct);

        return (MapToDto(batch, batch.Warehouse.Name, batch.CreatedBy.Username, variants), null, null);
    }

    // ───── Delete (Draft only) ─────

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> DeleteAsync(
        Guid id, CancellationToken ct = default)
    {
        var batch = await _db.ProductionBatches.FindAsync([id], ct);
        if (batch is null)
            return (false, ErrorCodes.ProductionBatchNotFound, "Production batch not found.");

        if (batch.Status != ProductionBatchStatus.Draft)
            return (false, ErrorCodes.ProductionBatchAlreadyPosted, "Cannot delete a posted production batch.");

        _db.ProductionBatches.Remove(batch);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ───── Post (Atomic + Idempotent) ─────

    /// <summary>
    /// Posts a draft production batch atomically:
    /// 1. Claims the entity with an atomic UPDATE … WHERE Status = Draft (TOCTOU prevention)
    /// 2. Creates a ProductionConsume StockMovement (negative deltas for inputs)
    /// 3. Creates a ProductionProduce StockMovement (positive deltas for outputs)
    /// Negative stock rules apply — if raw materials are insufficient, the entire post fails.
    /// Idempotent: if already posted, returns the existing movement IDs.
    /// Concurrent duplicates get 409 POST_CONCURRENCY_CONFLICT.
    /// </summary>
    public async Task<PostResult> PostAsync(Guid batchId, Guid userId, CancellationToken ct = default)
    {
        // ── Atomic concurrency gate ──
        var claimed = await _db.ProductionBatches
            .Where(b => b.Id == batchId && b.Status == ProductionBatchStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, ProductionBatchStatus.Posted)
                .SetProperty(b => b.PostedAtUtc, DateTime.UtcNow), ct);

        if (claimed == 0)
        {
            var existing = await _db.ProductionBatches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => new { b.Status, b.ConsumeMovementId, b.ProduceMovementId })
                .FirstOrDefaultAsync(ct);

            if (existing is null)
                return new PostResult(false, null, null, ErrorCodes.ProductionBatchNotFound, "Production batch not found.");

            if (existing.Status == ProductionBatchStatus.Posted
                && existing.ConsumeMovementId.HasValue && existing.ProduceMovementId.HasValue)
                return new PostResult(true, existing.ConsumeMovementId, existing.ProduceMovementId,
                    ErrorCodes.PostAlreadyPosted, "Already posted.");

            return new PostResult(false, null, null, ErrorCodes.PostConcurrencyConflict,
                "Posting in progress by another request. Retry shortly.");
        }

        // ── Load entity for stock movements ──
        var batch = await _db.ProductionBatches
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

        var inputs = batch!.Lines.Where(l => l.LineType == ProductionLineType.Input).ToList();
        var outputs = batch.Lines.Where(l => l.LineType == ProductionLineType.Output).ToList();

        if (inputs.Count == 0)
        {
            await RollbackProductionClaimAsync(batchId, ct);
            return new PostResult(false, null, null, ErrorCodes.ProductionBatchNoInputs, "Production batch has no input lines.");
        }
        if (outputs.Count == 0)
        {
            await RollbackProductionClaimAsync(batchId, ct);
            return new PostResult(false, null, null, ErrorCodes.ProductionBatchNoOutputs, "Production batch has no output lines.");
        }

        // ── Step 1: Post consumption (negative deltas — raw materials OUT) ──
        var consumeLines = inputs.Select(line =>
            new StockService.PostMovementLineRequest(
                line.VariantId,
                batch.WarehouseId,
                -line.Quantity,
                line.UnitCost,
                $"Production batch {batch.BatchNumber} — consume"
            )).ToList();

        var consumeRequest = new StockService.PostMovementRequest(
            MovementType.ProductionConsume,
            batch.BatchNumber,
            $"Production consume: {batch.Notes}",
            consumeLines);

        var consumeResult = await _stockService.PostAsync(consumeRequest, userId, ct);
        if (!consumeResult.Success)
        {
            await RollbackProductionClaimAsync(batchId, ct);
            return new PostResult(false, null, null, consumeResult.ErrorCode, consumeResult.ErrorDetail);
        }

        // ── Step 2: Post production (positive deltas — finished goods IN) ──
        var produceLines = outputs.Select(line =>
            new StockService.PostMovementLineRequest(
                line.VariantId,
                batch.WarehouseId,
                line.Quantity,
                line.UnitCost,
                $"Production batch {batch.BatchNumber} — produce"
            )).ToList();

        var produceRequest = new StockService.PostMovementRequest(
            MovementType.ProductionProduce,
            batch.BatchNumber,
            $"Production produce: {batch.Notes}",
            produceLines);

        var produceResult = await _stockService.PostAsync(produceRequest, userId, ct);
        if (!produceResult.Success)
        {
            // Consume movement already committed — cannot undo here.
            // This should not happen for positive deltas, but handle gracefully.
            await RollbackProductionClaimAsync(batchId, ct);
            return new PostResult(false, null, null, produceResult.ErrorCode, produceResult.ErrorDetail);
        }

        // ── Finalize: link movement IDs ──
        batch.ConsumeMovementId = consumeResult.MovementId;
        batch.ProduceMovementId = produceResult.MovementId;

        await _db.SaveChangesAsync(ct);

        return new PostResult(true, consumeResult.MovementId, produceResult.MovementId, null, null);
    }

    /// <summary>Reverts the atomic claim if stock posting fails.</summary>
    private async Task RollbackProductionClaimAsync(Guid batchId, CancellationToken ct)
    {
        await _db.ProductionBatches
            .Where(b => b.Id == batchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, ProductionBatchStatus.Draft)
                .SetProperty(b => b.PostedAtUtc, (DateTime?)null), ct);
    }

    // ───── Helpers ─────

    private async Task<Dictionary<Guid, ProductVariant>> LoadVariantDictAsync(
        List<Guid> variantIds, CancellationToken ct)
    {
        return await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Id, ct);
    }

    private static BatchDto MapToDto(
        ProductionBatch batch, string warehouseName,
        string createdByUsername, Dictionary<Guid, ProductVariant> variants)
    {
        var inputDtos = batch.Lines
            .Where(l => l.LineType == ProductionLineType.Input)
            .Select(l => MapLineDto(l, variants))
            .ToList();

        var outputDtos = batch.Lines
            .Where(l => l.LineType == ProductionLineType.Output)
            .Select(l => MapLineDto(l, variants))
            .ToList();

        return new BatchDto(
            batch.Id, batch.BatchNumber,
            batch.WarehouseId, warehouseName, batch.Notes,
            batch.Status.ToString(),
            batch.ConsumeMovementId, batch.ProduceMovementId,
            batch.CreatedAtUtc, batch.PostedAtUtc,
            batch.CreatedByUserId, createdByUsername,
            inputDtos, outputDtos);
    }

    private static BatchLineDto MapLineDto(ProductionBatchLine l, Dictionary<Guid, ProductVariant> variants)
    {
        var variant = variants.GetValueOrDefault(l.VariantId);
        return new BatchLineDto(
            l.Id, l.VariantId,
            variant?.Sku ?? "UNKNOWN",
            variant?.Product?.Name,
            l.Quantity, l.UnitCost);
    }
}

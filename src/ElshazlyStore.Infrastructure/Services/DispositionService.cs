using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles inventory disposition CRUD, approval, posting, and voiding.
/// Dispositions handle pre-sale inventory issues: damage, theft, expiry, defects.
/// Posting creates a Disposition stock movement that moves qty out of the source
/// warehouse and into special warehouses (Scrap/Quarantine/Rework) or writes off (theft/loss).
/// </summary>
public sealed class DispositionService
{
    private readonly AppDbContext _db;
    private readonly StockService _stockService;

    public DispositionService(AppDbContext db, StockService stockService)
    {
        _db = db;
        _stockService = stockService;
    }

    // ───── DTOs ─────

    public sealed record CreateDispositionRequest(
        Guid WarehouseId,
        DateTime? DispositionDateUtc,
        string? Notes,
        List<DispositionLineRequest> Lines);

    public sealed record DispositionLineRequest(
        Guid VariantId,
        decimal Quantity,
        Guid ReasonCodeId,
        DispositionType DispositionType,
        string? Notes);

    public sealed record UpdateDispositionRequest(
        Guid? WarehouseId,
        string? Notes,
        List<DispositionLineRequest>? Lines);

    public sealed record DispositionDto(
        Guid Id, string DispositionNumber,
        DateTime DispositionDateUtc,
        Guid WarehouseId, string WarehouseName,
        Guid CreatedByUserId, string CreatedByUsername,
        string? Notes, string Status,
        Guid? StockMovementId,
        Guid? ApprovedByUserId, string? ApprovedByUsername,
        DateTime? ApprovedAtUtc,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        Guid? PostedByUserId,
        List<DispositionLineDto> Lines);

    public sealed record DispositionLineDto(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity,
        Guid ReasonCodeId, string ReasonCodeCode, string ReasonCodeNameAr,
        bool RequiresManagerApproval,
        string DispositionType, string? Notes);

    public sealed record PostResult(
        bool Success, Guid? StockMovementId,
        string? ErrorCode, string? ErrorDetail);

    // ───── Create ─────

    public async Task<(DispositionDto? Disposition, string? ErrorCode, string? ErrorDetail)> CreateAsync(
        CreateDispositionRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Lines is not { Count: > 0 })
            return (null, ErrorCodes.DispositionEmpty, "Disposition must have at least one line.");

        // Validate warehouse
        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive, ct);
        if (warehouse is null)
            return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");

        // Validate line data
        var validationError = await ValidateLinesAsync(request.Lines, ct);
        if (validationError is not null)
            return (null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);

        // Validate disposition types (only Scrap, Quarantine, WriteOff, Rework allowed for pre-sale)
        foreach (var line in request.Lines)
        {
            if (line.DispositionType is DispositionType.ReturnToVendor or DispositionType.ReturnToStock)
                return (null, ErrorCodes.DispositionInvalidType,
                    $"Disposition type '{line.DispositionType}' is not allowed for pre-sale dispositions. Use Scrap, Quarantine, WriteOff, or Rework.");
        }

        // Generate disposition number: DISP-NNNNNN
        string dispositionNumber;
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            await EnsureDispositionSequenceAsync(ct);
            var seq = await _db.Database
                .SqlQueryRaw<long>("SELECT nextval('disposition_number_seq') AS \"Value\"")
                .SingleAsync(ct);
            dispositionNumber = $"DISP-{seq:D6}";
        }
        else
        {
            // Fallback for SQLite (tests)
            var existingCount = await _db.InventoryDispositions
                .Where(d => d.DispositionNumber.StartsWith("DISP-")
                    && d.DispositionNumber.Length == 11)
                .CountAsync(ct);
            dispositionNumber = $"DISP-{existingCount + 1:D6}";
            while (await _db.InventoryDispositions.AnyAsync(d => d.DispositionNumber == dispositionNumber, ct))
            {
                existingCount++;
                dispositionNumber = $"DISP-{existingCount + 1:D6}";
            }
        }

        var disposition = new InventoryDisposition
        {
            Id = Guid.NewGuid(),
            DispositionNumber = dispositionNumber,
            DispositionDateUtc = request.DispositionDateUtc ?? DateTime.UtcNow,
            WarehouseId = request.WarehouseId,
            Notes = request.Notes,
            Status = DispositionStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        foreach (var line in request.Lines)
        {
            disposition.Lines.Add(new InventoryDispositionLine
            {
                Id = Guid.NewGuid(),
                InventoryDispositionId = disposition.Id,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                ReasonCodeId = line.ReasonCodeId,
                DispositionType = line.DispositionType,
                Notes = line.Notes,
            });
        }

        _db.InventoryDispositions.Add(disposition);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FindAsync([userId], ct);
        var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await LoadVariantDictAsync(variantIds, ct);
        var reasonCodeIds = request.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var reasons = await LoadReasonCodeDictAsync(reasonCodeIds, ct);

        return (MapToDto(disposition, warehouse.Name, user!.Username, null, variants, reasons), null, null);
    }

    // ───── Get ─────

    public async Task<DispositionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var disp = await _db.InventoryDispositions
            .Include(d => d.Lines)
            .Include(d => d.Warehouse)
            .Include(d => d.CreatedBy)
            .Include(d => d.ApprovedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (disp is null) return null;

        var variantIds = disp.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await LoadVariantDictAsync(variantIds, ct);
        var reasonIds = disp.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var reasons = await LoadReasonCodeDictAsync(reasonIds, ct);

        return MapToDto(disp, disp.Warehouse.Name, disp.CreatedBy.Username,
            disp.ApprovedBy?.Username, variants, reasons);
    }

    // ───── List / Search ─────

    public async Task<(List<DispositionDto> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize, string? sort,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.InventoryDispositions
            .Include(d => d.Lines)
            .Include(d => d.Warehouse)
            .Include(d => d.CreatedBy)
            .Include(d => d.ApprovedBy)
            .AsNoTracking()
            .AsQueryable();

        query = query.ApplySearch(_db.Database, search,
            d => d.DispositionNumber,
            d => d.Warehouse.Name,
            d => d.Warehouse.Code,
            d => d.CreatedBy.Username,
            d => d.Notes);

        query = sort?.ToLowerInvariant() switch
        {
            "number" => query.OrderBy(d => d.DispositionNumber),
            "number_desc" => query.OrderByDescending(d => d.DispositionNumber),
            "date" => query.OrderBy(d => d.DispositionDateUtc),
            "date_desc" => query.OrderByDescending(d => d.DispositionDateUtc),
            "status" => query.OrderBy(d => d.Status),
            "created" => query.OrderBy(d => d.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(d => d.CreatedAtUtc),
            _ => query.OrderByDescending(d => d.CreatedAtUtc),
        };

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var allVariantIds = items.SelectMany(d => d.Lines).Select(l => l.VariantId).Distinct().ToList();
        var allVariants = await LoadVariantDictAsync(allVariantIds, ct);
        var allReasonIds = items.SelectMany(d => d.Lines).Select(l => l.ReasonCodeId).Distinct().ToList();
        var allReasons = await LoadReasonCodeDictAsync(allReasonIds, ct);

        var dtos = items.Select(d => MapToDto(
            d, d.Warehouse.Name, d.CreatedBy.Username,
            d.ApprovedBy?.Username, allVariants, allReasons)).ToList();
        return (dtos, totalCount);
    }

    // ───── Update (Draft only) ─────

    public async Task<(DispositionDto? Disposition, string? ErrorCode, string? ErrorDetail)> UpdateAsync(
        Guid id, UpdateDispositionRequest request, CancellationToken ct = default)
    {
        var disp = await _db.InventoryDispositions
            .Include(d => d.Warehouse)
            .Include(d => d.CreatedBy)
            .Include(d => d.ApprovedBy)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (disp is null)
            return (null, ErrorCodes.DispositionNotFound, "Disposition not found.");

        if (disp.Status != DispositionStatus.Draft)
            return (null, ErrorCodes.DispositionAlreadyPosted, "Cannot modify a posted or voided disposition.");

        if (request.WarehouseId.HasValue)
        {
            var warehouse = await _db.Warehouses
                .FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value && w.IsActive, ct);
            if (warehouse is null)
                return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");
            disp.WarehouseId = request.WarehouseId.Value;
            disp.Warehouse = warehouse;
        }

        if (request.Notes is not null)
            disp.Notes = request.Notes;

        // Replace lines if provided
        if (request.Lines is not null)
        {
            if (request.Lines.Count == 0)
                return (null, ErrorCodes.DispositionEmpty, "Disposition must have at least one line.");

            var validationError = await ValidateLinesAsync(request.Lines, ct);
            if (validationError is not null)
                return (null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);

            foreach (var line in request.Lines)
            {
                if (line.DispositionType is DispositionType.ReturnToVendor or DispositionType.ReturnToStock)
                    return (null, ErrorCodes.DispositionInvalidType,
                        $"Disposition type '{line.DispositionType}' is not allowed for pre-sale dispositions.");
            }

            // Clear approval if lines change (may need re-approval)
            disp.ApprovedByUserId = null;
            disp.ApprovedAtUtc = null;

            // Delete existing lines
            await _db.InventoryDispositionLines
                .Where(l => l.InventoryDispositionId == disp.Id)
                .ExecuteDeleteAsync(ct);

            foreach (var line in request.Lines)
            {
                _db.InventoryDispositionLines.Add(new InventoryDispositionLine
                {
                    Id = Guid.NewGuid(),
                    InventoryDispositionId = disp.Id,
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    ReasonCodeId = line.ReasonCodeId,
                    DispositionType = line.DispositionType,
                    Notes = line.Notes,
                });
            }
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
        var disp = await _db.InventoryDispositions.FindAsync([id], ct);
        if (disp is null)
            return (false, ErrorCodes.DispositionNotFound, "Disposition not found.");

        if (disp.Status != DispositionStatus.Draft)
            return (false, ErrorCodes.DispositionAlreadyPosted, "Cannot delete a posted or voided disposition.");

        _db.InventoryDispositions.Remove(disp);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ───── Approve ─────

    /// <summary>
    /// Approves a draft disposition that contains lines requiring manager approval.
    /// Records ApprovedByUserId + ApprovedAtUtc for audit trail.
    /// </summary>
    public async Task<PostResult> ApproveAsync(Guid dispositionId, Guid approverId, CancellationToken ct = default)
    {
        var disp = await _db.InventoryDispositions
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == dispositionId, ct);

        if (disp is null)
            return new PostResult(false, null, ErrorCodes.DispositionNotFound, "Disposition not found.");

        if (disp.Status != DispositionStatus.Draft)
            return new PostResult(false, null, ErrorCodes.DispositionAlreadyPosted, "Cannot approve a posted or voided disposition.");

        if (disp.ApprovedByUserId.HasValue)
            return new PostResult(true, null, null, "Already approved.");

        disp.ApprovedByUserId = approverId;
        disp.ApprovedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new PostResult(true, null, null, null);
    }

    // ───── Post (Atomic + Idempotent) ─────

    /// <summary>
    /// Posts a draft disposition atomically:
    /// 1. Claims the entity with an atomic UPDATE … WHERE Status = Draft (TOCTOU prevention)
    /// 2. Validates manager approval if required
    /// 3. Creates a Disposition stock movement:
    ///    - Scrap: move qty OUT of source into SCRAP warehouse
    ///    - Quarantine: move qty OUT of source into QUARANTINE warehouse
    ///    - Rework: move qty OUT of source into REWORK warehouse
    ///    - WriteOff: remove qty from source warehouse (no destination)
    /// </summary>
    public async Task<PostResult> PostAsync(Guid dispositionId, Guid userId, CancellationToken ct = default)
    {
        // ── Atomic concurrency gate ──
        var claimed = await _db.InventoryDispositions
            .Where(d => d.Id == dispositionId && d.Status == DispositionStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DispositionStatus.Posted)
                .SetProperty(d => d.PostedAtUtc, DateTime.UtcNow)
                .SetProperty(d => d.PostedByUserId, userId), ct);

        if (claimed == 0)
        {
            var existing = await _db.InventoryDispositions
                .AsNoTracking()
                .Where(d => d.Id == dispositionId)
                .Select(d => new { d.Status, d.StockMovementId })
                .FirstOrDefaultAsync(ct);

            if (existing is null)
                return new PostResult(false, null, ErrorCodes.DispositionNotFound, "Disposition not found.");

            // Already fully posted → idempotent success
            if (existing.Status == DispositionStatus.Posted && existing.StockMovementId.HasValue)
                return new PostResult(true, existing.StockMovementId, ErrorCodes.PostAlreadyPosted, "Already posted.");

            if (existing.Status == DispositionStatus.Voided)
                return new PostResult(false, null, ErrorCodes.DispositionAlreadyVoided, "Disposition has been voided.");

            // Posted but StockMovementId null → another request is mid-posting
            return new PostResult(false, null, ErrorCodes.PostConcurrencyConflict,
                "Posting in progress by another request. Retry shortly.");
        }

        // ── Load entity for stock movement ──
        var disposition = await _db.InventoryDispositions
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == dispositionId, ct);

        if (disposition!.Lines.Count == 0)
        {
            await RollbackDispositionClaimAsync(dispositionId, ct);
            return new PostResult(false, null, ErrorCodes.DispositionEmpty, "Disposition has no lines.");
        }

        // Validate reason codes are still active at posting time
        var reasonCodeIds = disposition.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var reasonCodes = await _db.ReasonCodes
            .Where(rc => reasonCodeIds.Contains(rc.Id))
            .ToDictionaryAsync(rc => rc.Id, ct);

        foreach (var line in disposition.Lines)
        {
            if (!reasonCodes.TryGetValue(line.ReasonCodeId, out var rc) || !rc.IsActive)
            {
                await RollbackDispositionClaimAsync(dispositionId, ct);
                return new PostResult(false, null, ErrorCodes.ReasonCodeInactive,
                    $"Reason code '{rc?.Code ?? line.ReasonCodeId.ToString()}' is inactive.");
            }
        }

        // Check manager approval requirement
        var needsApproval = disposition.Lines.Any(l =>
            reasonCodes.TryGetValue(l.ReasonCodeId, out var rc) && rc.RequiresManagerApproval);
        if (needsApproval && !disposition.ApprovedByUserId.HasValue)
        {
            await RollbackDispositionClaimAsync(dispositionId, ct);
            return new PostResult(false, null, ErrorCodes.DispositionRequiresApproval,
                "This disposition contains lines requiring manager approval. Approve before posting.");
        }

        // ── Resolve destination warehouses ──
        var specialWarehouses = await _db.Warehouses
            .Where(w => w.Code == "QUARANTINE" || w.Code == "SCRAP" || w.Code == "REWORK")
            .ToDictionaryAsync(w => w.Code, ct);

        // ── Create Disposition stock movement ──
        var stockLines = new List<StockService.PostMovementLineRequest>();
        foreach (var line in disposition.Lines)
        {
            var reason = reasonCodes.GetValueOrDefault(line.ReasonCodeId);
            var reasonText = $"Disposition {disposition.DispositionNumber} [{line.DispositionType}] {reason?.Code ?? ""}";

            switch (line.DispositionType)
            {
                case DispositionType.Scrap:
                {
                    if (!specialWarehouses.TryGetValue("SCRAP", out var scrapWh))
                    {
                        await RollbackDispositionClaimAsync(dispositionId, ct);
                        return new PostResult(false, null, ErrorCodes.DestinationWarehouseNotFound,
                            "SCRAP warehouse not found. Please ensure it is seeded.");
                    }
                    // Transfer: out of source, into scrap
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId, disposition.WarehouseId, -line.Quantity, null, reasonText));
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId, scrapWh.Id, line.Quantity, null, reasonText));
                    break;
                }
                case DispositionType.Quarantine:
                {
                    if (!specialWarehouses.TryGetValue("QUARANTINE", out var quarantineWh))
                    {
                        await RollbackDispositionClaimAsync(dispositionId, ct);
                        return new PostResult(false, null, ErrorCodes.DestinationWarehouseNotFound,
                            "QUARANTINE warehouse not found. Please ensure it is seeded.");
                    }
                    // Transfer: out of source, into quarantine
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId, disposition.WarehouseId, -line.Quantity, null, reasonText));
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId, quarantineWh.Id, line.Quantity, null, reasonText));
                    break;
                }
                case DispositionType.Rework:
                {
                    if (!specialWarehouses.TryGetValue("REWORK", out var reworkWh))
                    {
                        await RollbackDispositionClaimAsync(dispositionId, ct);
                        return new PostResult(false, null, ErrorCodes.DestinationWarehouseNotFound,
                            "REWORK warehouse not found. Please ensure it is seeded.");
                    }
                    // Transfer: out of source, into rework
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId, disposition.WarehouseId, -line.Quantity, null, reasonText));
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId, reworkWh.Id, line.Quantity, null, reasonText));
                    break;
                }
                case DispositionType.WriteOff:
                {
                    // WriteOff (theft/loss): remove qty from source, no destination
                    stockLines.Add(new StockService.PostMovementLineRequest(
                        line.VariantId, disposition.WarehouseId, -line.Quantity, null, reasonText));
                    break;
                }
                default:
                {
                    await RollbackDispositionClaimAsync(dispositionId, ct);
                    return new PostResult(false, null, ErrorCodes.DispositionInvalidType,
                        $"Disposition type '{line.DispositionType}' is not supported for pre-sale dispositions.");
                }
            }
        }

        if (stockLines.Count > 0)
        {
            var movementRequest = new StockService.PostMovementRequest(
                MovementType.Disposition,
                disposition.DispositionNumber,
                $"Inventory disposition {disposition.DispositionNumber}",
                stockLines);

            var stockResult = await _stockService.PostAsync(movementRequest, userId, ct);
            if (!stockResult.Success)
            {
                await RollbackDispositionClaimAsync(dispositionId, ct);
                return new PostResult(false, null, stockResult.ErrorCode, stockResult.ErrorDetail);
            }

            disposition.StockMovementId = stockResult.MovementId;
        }

        await _db.SaveChangesAsync(ct);

        return new PostResult(true, disposition.StockMovementId, null, null);
    }

    // ───── Void (Draft only) ─────

    /// <summary>
    /// Voids a draft disposition. Posted dispositions CANNOT be voided.
    /// </summary>
    public async Task<PostResult> VoidAsync(Guid dispositionId, Guid userId, CancellationToken ct = default)
    {
        var disp = await _db.InventoryDispositions
            .FirstOrDefaultAsync(d => d.Id == dispositionId, ct);

        if (disp is null)
            return new PostResult(false, null, ErrorCodes.DispositionNotFound, "Disposition not found.");

        if (disp.Status == DispositionStatus.Voided)
            return new PostResult(false, null, ErrorCodes.DispositionAlreadyVoided, "Disposition already voided.");

        if (disp.Status == DispositionStatus.Posted)
            return new PostResult(false, null, ErrorCodes.DispositionVoidNotAllowedAfterPost,
                "Cannot void a posted disposition. Posted dispositions are permanent.");

        disp.Status = DispositionStatus.Voided;
        disp.VoidedAtUtc = DateTime.UtcNow;
        disp.VoidedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        return new PostResult(true, null, null, null);
    }

    // ───── Validation helpers ─────

    private async Task<(string ErrorCode, string ErrorDetail)?> ValidateLinesAsync(
        List<DispositionLineRequest> lines, CancellationToken ct)
    {
        // Validate all variants exist
        var variantIds = lines.Select(l => l.VariantId).Distinct().ToList();
        var existingVariants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var missing = variantIds.Except(existingVariants).ToList();
        if (missing.Count > 0)
            return (ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

        // Validate reason codes
        var reasonCodeIds = lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var activeReasonCodes = await _db.ReasonCodes
            .Where(rc => reasonCodeIds.Contains(rc.Id))
            .ToListAsync(ct);
        var missingReasons = reasonCodeIds.Except(activeReasonCodes.Select(rc => rc.Id)).ToList();
        if (missingReasons.Count > 0)
            return (ErrorCodes.ReasonCodeNotFound, $"Reason code(s) not found: {string.Join(", ", missingReasons)}");
        var inactiveReasons = activeReasonCodes.Where(rc => !rc.IsActive).ToList();
        if (inactiveReasons.Count > 0)
            return (ErrorCodes.ReasonCodeInactive, $"Reason code(s) inactive: {string.Join(", ", inactiveReasons.Select(r => r.Code))}");

        // Validate positive quantities
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                return (ErrorCodes.ValidationFailed, "Line quantity must be positive.");
        }

        return null;
    }

    /// <summary>Reverts the atomic claim if stock posting fails.</summary>
    private async Task RollbackDispositionClaimAsync(Guid dispositionId, CancellationToken ct)
    {
        await _db.InventoryDispositions
            .Where(d => d.Id == dispositionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DispositionStatus.Draft)
                .SetProperty(d => d.PostedAtUtc, (DateTime?)null)
                .SetProperty(d => d.PostedByUserId, (Guid?)null), ct);
    }

    private async Task EnsureDispositionSequenceAsync(CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE SEQUENCE IF NOT EXISTS disposition_number_seq START WITH 1 INCREMENT BY 1;", ct);
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

    private static DispositionDto MapToDto(
        InventoryDisposition disp, string warehouseName,
        string createdByUsername, string? approvedByUsername,
        Dictionary<Guid, ProductVariant> variants,
        Dictionary<Guid, ReasonCode> reasons)
    {
        var lineDtos = disp.Lines.Select(l =>
        {
            var variant = variants.GetValueOrDefault(l.VariantId);
            var reason = reasons.GetValueOrDefault(l.ReasonCodeId);
            return new DispositionLineDto(
                l.Id, l.VariantId,
                variant?.Sku ?? "UNKNOWN",
                variant?.Product?.Name,
                l.Quantity,
                l.ReasonCodeId,
                reason?.Code ?? "UNKNOWN",
                reason?.NameAr ?? "",
                reason?.RequiresManagerApproval ?? false,
                l.DispositionType.ToString(),
                l.Notes);
        }).ToList();

        return new DispositionDto(
            disp.Id, disp.DispositionNumber,
            disp.DispositionDateUtc,
            disp.WarehouseId, warehouseName,
            disp.CreatedByUserId, createdByUsername,
            disp.Notes, disp.Status.ToString(),
            disp.StockMovementId,
            disp.ApprovedByUserId, approvedByUsername,
            disp.ApprovedAtUtc,
            disp.CreatedAtUtc, disp.PostedAtUtc,
            disp.PostedByUserId,
            lineDtos);
    }
}

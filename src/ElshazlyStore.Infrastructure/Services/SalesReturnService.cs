using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles sales return CRUD, posting, and voiding.
/// Posting creates a SaleReturnReceipt stock movement (positive deltas = stock in)
/// based on DispositionType, and creates a CreditNote ledger entry for AR.
/// </summary>
public sealed class SalesReturnService
{
    private readonly AppDbContext _db;
    private readonly StockService _stockService;
    private readonly AccountingService _accountingService;

    public SalesReturnService(AppDbContext db, StockService stockService, AccountingService accountingService)
    {
        _db = db;
        _stockService = stockService;
        _accountingService = accountingService;
    }

    // ───── DTOs ─────

    public sealed record CreateReturnRequest(
        Guid WarehouseId,
        Guid? CustomerId,
        Guid? OriginalSalesInvoiceId,
        DateTime? ReturnDateUtc,
        string? Notes,
        List<ReturnLineRequest> Lines);

    public sealed record ReturnLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal UnitPrice,
        Guid ReasonCodeId,
        DispositionType DispositionType,
        string? Notes);

    public sealed record UpdateReturnRequest(
        Guid? WarehouseId,
        Guid? CustomerId,
        bool ClearCustomer,
        Guid? OriginalSalesInvoiceId,
        bool ClearOriginalInvoice,
        string? Notes,
        List<ReturnLineRequest>? Lines);

    public sealed record ReturnDto(
        Guid Id, string ReturnNumber,
        DateTime ReturnDateUtc,
        Guid? CustomerId, string? CustomerName,
        Guid? OriginalSalesInvoiceId, string? OriginalInvoiceNumber,
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
        decimal Quantity, decimal UnitPrice, decimal LineTotal,
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
            return (null, ErrorCodes.SalesReturnEmpty, "Sales return must have at least one line.");

        // Validate warehouse
        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive, ct);
        if (warehouse is null)
            return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");

        // Validate customer if provided
        string? customerName = null;
        if (request.CustomerId.HasValue)
        {
            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value, ct);
            if (customer is null)
                return (null, ErrorCodes.CustomerNotFound, "Customer not found.");
            customerName = customer.Name;
        }

        // Validate original sales invoice if provided
        string? originalInvoiceNumber = null;
        if (request.OriginalSalesInvoiceId.HasValue)
        {
            var origInvoice = await _db.SalesInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(si => si.Id == request.OriginalSalesInvoiceId.Value, ct);
            if (origInvoice is null)
                return (null, ErrorCodes.SalesInvoiceNotFound, "Original sales invoice not found.");
            if (origInvoice.Status != SalesInvoiceStatus.Posted)
                return (null, ErrorCodes.ValidationFailed, "Original sales invoice must be posted.");
            originalInvoiceNumber = origInvoice.InvoiceNumber;
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

        // Validate positive quantities/prices and allowed dispositions
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
            if (line.UnitPrice < 0)
                return (null, ErrorCodes.ValidationFailed, "Line unit price cannot be negative.");

            var dispError = ValidateDisposition(line.DispositionType);
            if (dispError is not null)
                return (null, dispError.Value.ErrorCode, dispError.Value.ErrorDetail);
        }

        // Validate return qty vs sold qty if original invoice provided
        if (request.OriginalSalesInvoiceId.HasValue)
        {
            var validationError = await ValidateReturnQtyAsync(
                request.OriginalSalesInvoiceId.Value, request.Lines, null, ct);
            if (validationError is not null)
                return (null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);
        }

        // Generate return number: RET-NNNNNN
        string returnNumber;
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            // Create sequence if not exists, then use it
            await EnsureReturnSequenceAsync(ct);
            var seq = await _db.Database
                .SqlQueryRaw<long>("SELECT nextval('sales_return_number_seq') AS \"Value\"")
                .SingleAsync(ct);
            returnNumber = $"RET-{seq:D6}";
        }
        else
        {
            // Fallback for SQLite (tests)
            var existingCount = await _db.SalesReturns
                .Where(sr => sr.ReturnNumber.StartsWith("RET-")
                    && sr.ReturnNumber.Length == 10)
                .CountAsync(ct);
            returnNumber = $"RET-{existingCount + 1:D6}";
            while (await _db.SalesReturns.AnyAsync(sr => sr.ReturnNumber == returnNumber, ct))
            {
                existingCount++;
                returnNumber = $"RET-{existingCount + 1:D6}";
            }
        }

        var salesReturn = new SalesReturn
        {
            Id = Guid.NewGuid(),
            ReturnNumber = returnNumber,
            ReturnDateUtc = request.ReturnDateUtc ?? DateTime.UtcNow,
            CustomerId = request.CustomerId,
            OriginalSalesInvoiceId = request.OriginalSalesInvoiceId,
            WarehouseId = request.WarehouseId,
            Notes = request.Notes,
            Status = SalesReturnStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        decimal totalAmount = 0m;
        foreach (var line in request.Lines)
        {
            var lineTotal = line.Quantity * line.UnitPrice;
            salesReturn.Lines.Add(new SalesReturnLine
            {
                Id = Guid.NewGuid(),
                SalesReturnId = salesReturn.Id,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = lineTotal,
                ReasonCodeId = line.ReasonCodeId,
                DispositionType = line.DispositionType,
                Notes = line.Notes,
            });
            totalAmount += lineTotal;
        }
        salesReturn.TotalAmount = totalAmount;

        _db.SalesReturns.Add(salesReturn);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FindAsync([userId], ct);
        var variants = await LoadVariantDictAsync(variantIds, ct);
        var reasonDict = activeReasonCodes.ToDictionary(rc => rc.Id);

        return (MapToDto(salesReturn, customerName, originalInvoiceNumber,
            warehouse.Name, user!.Username, variants, reasonDict), null, null);
    }

    // ───── Get ─────

    public async Task<ReturnDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sr = await _db.SalesReturns
            .Include(r => r.Lines)
            .Include(r => r.Customer)
            .Include(r => r.OriginalSalesInvoice)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (sr is null) return null;

        var variantIds = sr.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await LoadVariantDictAsync(variantIds, ct);
        var reasonIds = sr.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var reasons = await LoadReasonCodeDictAsync(reasonIds, ct);

        return MapToDto(sr, sr.Customer?.Name, sr.OriginalSalesInvoice?.InvoiceNumber,
            sr.Warehouse.Name, sr.CreatedBy.Username, variants, reasons);
    }

    // ───── List / Search ─────

    public async Task<(List<ReturnDto> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize, string? sort,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.SalesReturns
            .Include(r => r.Lines)
            // Customer is intentionally NOT included here.
            // EF Core 8 + Npgsql pagination combined with the collection include (Lines)
            // does not reliably populate optional reference navigations (nullable FK).
            // Customer names are loaded explicitly below, matching the pattern used
            // for variants and reasons.
            .Include(r => r.OriginalSalesInvoice)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .AsNoTracking()
            .AsQueryable();

        query = query.ApplySearch(_db.Database, search,
            r => r.ReturnNumber,
            r => r.Customer!.Name,
            r => r.Customer!.Code,
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

        // Load customer names explicitly — same pattern as variants/reasons.
        // This guarantees correctness regardless of EF Core Include + pagination
        // behaviour with optional navigation properties.
        var customerIds = items
            .Where(r => r.CustomerId.HasValue)
            .Select(r => r.CustomerId!.Value)
            .Distinct()
            .ToList();
        var customerDict = customerIds.Count > 0
            ? await _db.Customers
                .Where(c => customerIds.Contains(c.Id))
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<Guid, string>();

        var dtos = items.Select(sr => MapToDto(
            sr,
            sr.CustomerId.HasValue ? customerDict.GetValueOrDefault(sr.CustomerId.Value) : null,
            sr.OriginalSalesInvoice?.InvoiceNumber,
            sr.Warehouse.Name, sr.CreatedBy.Username, allVariants, allReasons)).ToList();
        return (dtos, totalCount);
    }

    // ───── Update (Draft only) ─────

    public async Task<(ReturnDto? Return, string? ErrorCode, string? ErrorDetail)> UpdateAsync(
        Guid id, UpdateReturnRequest request, CancellationToken ct = default)
    {
        var sr = await _db.SalesReturns
            .Include(r => r.Customer)
            .Include(r => r.OriginalSalesInvoice)
            .Include(r => r.Warehouse)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (sr is null)
            return (null, ErrorCodes.SalesReturnNotFound, "Sales return not found.");

        if (sr.Status != SalesReturnStatus.Draft)
            return (null, ErrorCodes.SalesReturnAlreadyPosted, "Cannot modify a posted or voided sales return.");

        if (request.WarehouseId.HasValue)
        {
            var warehouse = await _db.Warehouses
                .FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value && w.IsActive, ct);
            if (warehouse is null)
                return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");
            sr.WarehouseId = request.WarehouseId.Value;
            sr.Warehouse = warehouse;
        }

        if (request.ClearCustomer)
        {
            sr.CustomerId = null;
            sr.Customer = null;
        }
        else if (request.CustomerId.HasValue)
        {
            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value, ct);
            if (customer is null)
                return (null, ErrorCodes.CustomerNotFound, "Customer not found.");
            sr.CustomerId = request.CustomerId.Value;
            sr.Customer = customer;
        }

        if (request.ClearOriginalInvoice)
        {
            sr.OriginalSalesInvoiceId = null;
            sr.OriginalSalesInvoice = null;
        }
        else if (request.OriginalSalesInvoiceId.HasValue)
        {
            // Validate existence and status via AsNoTracking — do NOT assign the navigation property.
            // sr.OriginalSalesInvoice is already tracked by EF from the Include above; assigning a
            // second (AsNoTracking) instance of the same SalesInvoice causes EF to throw an
            // InvalidOperationException ("another instance with the same key is already being tracked").
            // Updating only the FK is sufficient for EF to persist the relationship correctly.
            var origInvoice = await _db.SalesInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(si => si.Id == request.OriginalSalesInvoiceId.Value, ct);
            if (origInvoice is null)
                return (null, ErrorCodes.SalesInvoiceNotFound, "Original sales invoice not found.");
            if (origInvoice.Status != SalesInvoiceStatus.Posted)
                return (null, ErrorCodes.ValidationFailed, "Original sales invoice must be posted.");
            sr.OriginalSalesInvoiceId = request.OriginalSalesInvoiceId.Value;
            // intentionally not assigning sr.OriginalSalesInvoice — FK update is sufficient
        }

        if (request.Notes is not null)
            sr.Notes = request.Notes;

        // Replace lines if provided
        if (request.Lines is not null)
        {
            if (request.Lines.Count == 0)
                return (null, ErrorCodes.SalesReturnEmpty, "Sales return must have at least one line.");

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
                if (line.UnitPrice < 0)
                    return (null, ErrorCodes.ValidationFailed, "Line unit price cannot be negative.");

                var dispError = ValidateDisposition(line.DispositionType);
                if (dispError is not null)
                    return (null, dispError.Value.ErrorCode, dispError.Value.ErrorDetail);
            }

            // Validate return qty vs sold qty if original invoice
            var effectiveInvoiceId = request.ClearOriginalInvoice
                ? null
                : (request.OriginalSalesInvoiceId ?? sr.OriginalSalesInvoiceId);
            if (effectiveInvoiceId.HasValue)
            {
                var validationError = await ValidateReturnQtyAsync(
                    effectiveInvoiceId.Value, request.Lines, sr.Id, ct);
                if (validationError is not null)
                    return (null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);
            }

            // Delete existing lines
            await _db.SalesReturnLines
                .Where(l => l.SalesReturnId == sr.Id)
                .ExecuteDeleteAsync(ct);

            decimal totalAmount = 0m;
            foreach (var line in request.Lines)
            {
                var lineTotal = line.Quantity * line.UnitPrice;
                _db.SalesReturnLines.Add(new SalesReturnLine
                {
                    Id = Guid.NewGuid(),
                    SalesReturnId = sr.Id,
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = lineTotal,
                    ReasonCodeId = line.ReasonCodeId,
                    DispositionType = line.DispositionType,
                    Notes = line.Notes,
                });
                totalAmount += lineTotal;
            }
            sr.TotalAmount = totalAmount;
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
        var sr = await _db.SalesReturns.FindAsync([id], ct);
        if (sr is null)
            return (false, ErrorCodes.SalesReturnNotFound, "Sales return not found.");

        if (sr.Status != SalesReturnStatus.Draft)
            return (false, ErrorCodes.SalesReturnAlreadyPosted, "Cannot delete a posted or voided sales return.");

        _db.SalesReturns.Remove(sr);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ───── Post (Atomic + Idempotent) ─────

    /// <summary>
    /// Posts a draft sales return atomically:
    /// 1. Claims the entity with an atomic UPDATE … WHERE Status = Draft (TOCTOU prevention)
    /// 2. Validates return qty against original invoice (if linked)
    /// 3. Creates a SaleReturnReceipt stock movement based on DispositionType
    /// 4. Creates a CreditNote ledger entry if customer exists
    /// Idempotent: if already posted, returns the existing movement ID.
    /// Concurrent duplicates get 409 POST_CONCURRENCY_CONFLICT.
    /// </summary>
    public async Task<PostResult> PostAsync(Guid returnId, Guid userId, CancellationToken ct = default)
    {
        // ── Atomic concurrency gate ──
        var claimed = await _db.SalesReturns
            .Where(sr => sr.Id == returnId && sr.Status == SalesReturnStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(sr => sr.Status, SalesReturnStatus.Posted)
                .SetProperty(sr => sr.PostedAtUtc, DateTime.UtcNow)
                .SetProperty(sr => sr.PostedByUserId, userId), ct);

        if (claimed == 0)
        {
            var existing = await _db.SalesReturns
                .AsNoTracking()
                .Where(sr => sr.Id == returnId)
                .Select(sr => new { sr.Status, sr.StockMovementId })
                .FirstOrDefaultAsync(ct);

            if (existing is null)
                return new PostResult(false, null, ErrorCodes.SalesReturnNotFound, "Sales return not found.");

            // Already fully posted → idempotent success
            if (existing.Status == SalesReturnStatus.Posted && existing.StockMovementId.HasValue)
                return new PostResult(true, existing.StockMovementId, ErrorCodes.PostAlreadyPosted, "Already posted.");

            if (existing.Status == SalesReturnStatus.Voided)
                return new PostResult(false, null, ErrorCodes.SalesReturnAlreadyVoided, "Sales return has been voided.");

            // Posted but StockMovementId null → another request is mid-posting
            return new PostResult(false, null, ErrorCodes.PostConcurrencyConflict,
                "Posting in progress by another request. Retry shortly.");
        }

        // ── Load entity for stock movement ──
        var salesReturn = await _db.SalesReturns
            .Include(sr => sr.Lines)
            .FirstOrDefaultAsync(sr => sr.Id == returnId, ct);

        if (salesReturn!.Lines.Count == 0)
        {
            await RollbackReturnClaimAsync(returnId, ct);
            return new PostResult(false, null, ErrorCodes.SalesReturnEmpty, "Sales return has no lines.");
        }

        // Validate reason codes are still active at posting time
        var reasonCodeIds = salesReturn.Lines.Select(l => l.ReasonCodeId).Distinct().ToList();
        var reasonCodes = await _db.ReasonCodes
            .Where(rc => reasonCodeIds.Contains(rc.Id))
            .ToDictionaryAsync(rc => rc.Id, ct);
        foreach (var line in salesReturn.Lines)
        {
            if (!reasonCodes.TryGetValue(line.ReasonCodeId, out var rc) || !rc.IsActive)
            {
                await RollbackReturnClaimAsync(returnId, ct);
                return new PostResult(false, null, ErrorCodes.ReasonCodeInactive,
                    $"Reason code '{rc?.Code ?? line.ReasonCodeId.ToString()}' is inactive.");
            }
        }

        // Validate return qty if original invoice linked
        if (salesReturn.OriginalSalesInvoiceId.HasValue)
        {
            var lineRequests = salesReturn.Lines.Select(l =>
                new ReturnLineRequest(l.VariantId, l.Quantity, l.UnitPrice, l.ReasonCodeId, l.DispositionType, l.Notes))
                .ToList();
            var validationError = await ValidateReturnQtyAsync(
                salesReturn.OriginalSalesInvoiceId.Value, lineRequests, salesReturn.Id, ct);
            if (validationError is not null)
            {
                await RollbackReturnClaimAsync(returnId, ct);
                return new PostResult(false, null, validationError.Value.ErrorCode, validationError.Value.ErrorDetail);
            }
        }

        // Validate dispositions at posting time (belt & suspenders)
        foreach (var line in salesReturn.Lines)
        {
            var dispError = ValidateDisposition(line.DispositionType);
            if (dispError is not null)
            {
                await RollbackReturnClaimAsync(returnId, ct);
                return new PostResult(false, null, dispError.Value.ErrorCode, dispError.Value.ErrorDetail);
            }
        }

        // ── Create SaleReturnReceipt stock movement ──
        // RET 1 only allows ReturnToStock / Quarantine — both add positive delta.
        var stockLines = new List<StockService.PostMovementLineRequest>();
        foreach (var line in salesReturn.Lines)
        {
            stockLines.Add(new StockService.PostMovementLineRequest(
                line.VariantId,
                salesReturn.WarehouseId,
                line.Quantity, // Always positive — only RTS/Quarantine allowed
                line.UnitPrice,
                $"Return {salesReturn.ReturnNumber} [{line.DispositionType}]"
            ));
        }

        if (stockLines.Count > 0)
        {
            var movementRequest = new StockService.PostMovementRequest(
                MovementType.SaleReturnReceipt,
                salesReturn.ReturnNumber,
                $"Sales return {salesReturn.ReturnNumber}",
                stockLines);

            var stockResult = await _stockService.PostAsync(movementRequest, userId, ct);
            if (!stockResult.Success)
            {
                await RollbackReturnClaimAsync(returnId, ct);
                return new PostResult(false, null, stockResult.ErrorCode, stockResult.ErrorDetail);
            }

            salesReturn.StockMovementId = stockResult.MovementId;
        }

        await _db.SaveChangesAsync(ct);

        // ── Create CreditNote ledger entry (reduces outstanding) ──
        if (salesReturn.CustomerId.HasValue)
        {
            var entry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                PartyType = PartyType.Customer,
                PartyId = salesReturn.CustomerId.Value,
                EntryType = LedgerEntryType.CreditNote,
                Amount = -salesReturn.TotalAmount, // Negative = reduces outstanding
                Reference = $"Credit note from return {salesReturn.ReturnNumber}",
                RelatedInvoiceId = salesReturn.OriginalSalesInvoiceId,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId,
            };
            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
        }

        return new PostResult(true, salesReturn.StockMovementId, null, null);
    }

    // ───── Void (Draft only — RET 1 policy A1) ─────

    /// <summary>
    /// Voids a draft sales return. Posted returns CANNOT be voided (RET 1 policy A1).
    /// Void is a soft-cancel that preserves the record with a terminal Voided status.
    /// </summary>
    public async Task<PostResult> VoidAsync(Guid returnId, Guid userId, CancellationToken ct = default)
    {
        var sr = await _db.SalesReturns
            .FirstOrDefaultAsync(r => r.Id == returnId, ct);

        if (sr is null)
            return new PostResult(false, null, ErrorCodes.SalesReturnNotFound, "Sales return not found.");

        if (sr.Status == SalesReturnStatus.Voided)
            return new PostResult(false, null, ErrorCodes.SalesReturnAlreadyVoided, "Sales return already voided.");

        if (sr.Status == SalesReturnStatus.Posted)
            return new PostResult(false, null, ErrorCodes.SalesReturnVoidNotAllowedAfterPost,
                "Cannot void a posted sales return. Posted returns are permanent.");

        // Only Draft can be voided
        sr.Status = SalesReturnStatus.Voided;
        sr.VoidedAtUtc = DateTime.UtcNow;
        sr.VoidedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        return new PostResult(true, null, null, null);
    }

    // ───── Validation helpers ─────

    /// <summary>
    /// Validates that returned qty per variant does not exceed sold qty
    /// minus already-returned qty from other posted returns linked to the same invoice.
    /// </summary>
    private async Task<(string ErrorCode, string ErrorDetail)?> ValidateReturnQtyAsync(
        Guid originalInvoiceId, List<ReturnLineRequest> lines, Guid? excludeReturnId,
        CancellationToken ct)
    {
        // Get sold quantities per variant from the original invoice
        var invoiceLines = await _db.SalesInvoiceLines
            .Where(l => l.SalesInvoiceId == originalInvoiceId)
            .Select(l => new { l.VariantId, l.Quantity })
            .ToListAsync(ct);

        var soldQtys = invoiceLines
            .GroupBy(l => l.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        // Get already-returned quantities per variant from OTHER posted returns on this invoice
        var alreadyReturnedQuery = _db.SalesReturnLines
            .Where(l => l.SalesReturn.OriginalSalesInvoiceId == originalInvoiceId
                && l.SalesReturn.Status == SalesReturnStatus.Posted);

        if (excludeReturnId.HasValue)
            alreadyReturnedQuery = alreadyReturnedQuery.Where(l => l.SalesReturnId != excludeReturnId.Value);

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
            if (!soldQtys.TryGetValue(variantId, out var sold))
                return (ErrorCodes.ReturnQtyExceedsSold,
                    $"Variant {variantId} was not sold on the original invoice.");

            var previouslyReturned = alreadyReturned.GetValueOrDefault(variantId, 0m);
            var available = sold - previouslyReturned;

            if (requestedQty > available)
                return (ErrorCodes.ReturnQtyExceedsSold,
                    $"Variant {variantId}: requested return qty {requestedQty} exceeds available {available} (sold {sold}, already returned {previouslyReturned}).");
        }

        return null;
    }

    /// <summary>
    /// RET 1 disposition policy: only ReturnToStock and Quarantine are allowed.
    /// Scrap/WriteOff/Rework/ReturnToVendor deferred to RET 3 Dispositions.
    /// </summary>
    private static (string ErrorCode, string ErrorDetail)? ValidateDisposition(DispositionType disposition)
    {
        if (disposition is DispositionType.ReturnToStock or DispositionType.Quarantine)
            return null;

        return (ErrorCodes.SalesReturnDispositionNotAllowed,
            $"Disposition '{disposition}' is not allowed in RET 1. Only ReturnToStock and Quarantine are permitted. Use RET 3 Dispositions for Scrap, WriteOff, Rework, and ReturnToVendor.");
    }

    /// <summary>Reverts the atomic claim if stock posting fails.</summary>
    private async Task RollbackReturnClaimAsync(Guid returnId, CancellationToken ct)
    {
        await _db.SalesReturns
            .Where(sr => sr.Id == returnId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(sr => sr.Status, SalesReturnStatus.Draft)
                .SetProperty(sr => sr.PostedAtUtc, (DateTime?)null)
                .SetProperty(sr => sr.PostedByUserId, (Guid?)null), ct);
    }

    private async Task EnsureReturnSequenceAsync(CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE SEQUENCE IF NOT EXISTS sales_return_number_seq START WITH 1 INCREMENT BY 1;", ct);
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
        SalesReturn sr, string? customerName, string? originalInvoiceNumber,
        string warehouseName, string createdByUsername,
        Dictionary<Guid, ProductVariant> variants,
        Dictionary<Guid, ReasonCode> reasons)
    {
        var lineDtos = sr.Lines.Select(l =>
        {
            var variant = variants.GetValueOrDefault(l.VariantId);
            var reason = reasons.GetValueOrDefault(l.ReasonCodeId);
            return new ReturnLineDto(
                l.Id, l.VariantId,
                variant?.Sku ?? "UNKNOWN",
                variant?.Product?.Name,
                l.Quantity, l.UnitPrice, l.LineTotal,
                l.ReasonCodeId,
                reason?.Code ?? "UNKNOWN",
                reason?.NameAr ?? "",
                l.DispositionType.ToString(),
                l.Notes);
        }).ToList();

        return new ReturnDto(
            sr.Id, sr.ReturnNumber,
            sr.ReturnDateUtc,
            sr.CustomerId, customerName,
            sr.OriginalSalesInvoiceId, originalInvoiceNumber,
            sr.WarehouseId, warehouseName,
            sr.CreatedByUserId, createdByUsername,
            sr.Notes, sr.Status.ToString(),
            sr.StockMovementId,
            sr.TotalAmount,
            sr.CreatedAtUtc, sr.PostedAtUtc,
            sr.PostedByUserId,
            lineDtos);
    }
}

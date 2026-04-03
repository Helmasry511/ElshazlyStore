using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles sales invoice CRUD and posting.
/// Posting creates a SaleIssue stock movement (negative deltas = stock out)
/// and optionally a CustomerReceivable placeholder for AR.
/// </summary>
public sealed class SalesService
{
    private readonly AppDbContext _db;
    private readonly StockService _stockService;
    private readonly AccountingService _accountingService;

    public SalesService(AppDbContext db, StockService stockService, AccountingService accountingService)
    {
        _db = db;
        _stockService = stockService;
        _accountingService = accountingService;
    }

    // ───── DTOs ─────

    public sealed record CreateInvoiceRequest(
        Guid WarehouseId,
        Guid? CustomerId,
        DateTime? InvoiceDateUtc,
        string? Notes,
        List<InvoiceLineRequest> Lines);

    public sealed record InvoiceLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal UnitPrice,
        decimal? DiscountAmount);

    public sealed record UpdateInvoiceRequest(
        Guid? WarehouseId,
        Guid? CustomerId,
        bool ClearCustomer, // explicit flag to set customer to null
        string? Notes,
        List<InvoiceLineRequest>? Lines);

    public sealed record InvoiceDto(
        Guid Id, string InvoiceNumber,
        DateTime InvoiceDateUtc,
        Guid? CustomerId, string? CustomerName,
        Guid WarehouseId, string WarehouseName,
        Guid CashierUserId, string CashierUsername,
        string? Notes, string Status,
        Guid? StockMovementId,
        decimal TotalAmount,
        DateTime CreatedAtUtc, DateTime? PostedAtUtc,
        List<InvoiceLineDto> Lines,
        InvoicePaymentTraceDto? PaymentTrace);

    public sealed record InvoicePaymentTraceDto(
        decimal? PaidAmount,
        decimal? RemainingAmount,
        string? PaymentMethod,
        string? WalletName,
        string? PaymentReference,
        int PaymentCount);

    public sealed record InvoiceLineDto(
        Guid Id, Guid VariantId, string Sku, string? ProductName,
        decimal Quantity, decimal UnitPrice,
        decimal DiscountAmount, decimal LineTotal);

    public sealed record PostResult(
        bool Success, Guid? StockMovementId,
        string? ErrorCode, string? ErrorDetail);

    // ───── Create ─────

    public async Task<(InvoiceDto? Invoice, string? ErrorCode, string? ErrorDetail)> CreateAsync(
        CreateInvoiceRequest request, Guid cashierUserId, CancellationToken ct = default)
    {
        if (request.Lines is not { Count: > 0 })
            return (null, ErrorCodes.SalesInvoiceEmpty, "Sales invoice must have at least one line.");

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

        // Validate all variants exist
        var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var existingVariants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var missing = variantIds.Except(existingVariants).ToList();
        if (missing.Count > 0)
            return (null, ErrorCodes.VariantNotFound, $"Variant(s) not found: {string.Join(", ", missing)}");

        // Validate positive quantities/prices
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                return (null, ErrorCodes.ValidationFailed, "Line quantity must be positive.");
            if (line.UnitPrice < 0)
                return (null, ErrorCodes.ValidationFailed, "Line unit price cannot be negative.");
            if (line.DiscountAmount.HasValue && line.DiscountAmount.Value < 0)
                return (null, ErrorCodes.ValidationFailed, "Line discount cannot be negative.");
        }

        // Generate invoice number: INV-NNNNNN via PostgreSQL sequence (atomic, no race)
        string invoiceNumber;
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            var seq = await _db.Database
                .SqlQueryRaw<long>("SELECT nextval('sales_invoice_number_seq') AS \"Value\"")
                .SingleAsync(ct);
            invoiceNumber = $"INV-{seq:D6}";
        }
        else
        {
            // Fallback for SQLite (tests)
            var existingCount = await _db.SalesInvoices
                .Where(si => si.InvoiceNumber.StartsWith("INV-")
                    && si.InvoiceNumber.Length == 10)
                .CountAsync(ct);
            invoiceNumber = $"INV-{existingCount + 1:D6}";
            while (await _db.SalesInvoices.AnyAsync(si => si.InvoiceNumber == invoiceNumber, ct))
            {
                existingCount++;
                invoiceNumber = $"INV-{existingCount + 1:D6}";
            }
        }

        var invoiceDateUtc = NormalizeInvoiceDateUtc(request.InvoiceDateUtc);

        var invoice = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            InvoiceDateUtc = invoiceDateUtc,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            CashierUserId = cashierUserId,
            Notes = request.Notes,
            Status = SalesInvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
        };

        decimal totalAmount = 0m;
        foreach (var line in request.Lines)
        {
            var discount = line.DiscountAmount ?? 0m;
            var lineTotal = (line.Quantity * line.UnitPrice) - discount;

            invoice.Lines.Add(new SalesInvoiceLine
            {
                Id = Guid.NewGuid(),
                SalesInvoiceId = invoice.Id,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = discount,
                LineTotal = lineTotal,
            });

            totalAmount += lineTotal;
        }
        invoice.TotalAmount = totalAmount;

        _db.SalesInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        var cashier = await _db.Users.FindAsync([cashierUserId], ct);
        var variants = await LoadVariantDictAsync(variantIds, ct);

        return (MapToDto(invoice, customerName, warehouse.Name, cashier!.Username, variants, null), null, null);
    }

    // ───── Get ─────

    public async Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _db.SalesInvoices
            .Include(si => si.Lines)
            .Include(si => si.Customer)
            .Include(si => si.Warehouse)
            .Include(si => si.Cashier)
            .AsNoTracking()
            .FirstOrDefaultAsync(si => si.Id == id, ct);

        if (invoice is null) return null;

        var variantIds = invoice.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await LoadVariantDictAsync(variantIds, ct);
        var paymentTrace = await LoadPaymentTraceAsync(invoice, ct);

        return MapToDto(invoice, invoice.Customer?.Name, invoice.Warehouse.Name,
            invoice.Cashier.Username, variants, paymentTrace);
    }

    // ───── List / Search ─────

    public async Task<(List<InvoiceDto> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize, string? sort,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.SalesInvoices
            .Include(si => si.Lines)
            .Include(si => si.Customer)
            .Include(si => si.Warehouse)
            .Include(si => si.Cashier)
            .AsNoTracking()
            .AsQueryable();

        query = query.ApplySearch(_db.Database, search,
            si => si.InvoiceNumber,
            si => si.Customer!.Name,
            si => si.Cashier.Username,
            si => si.Notes);

        query = sort?.ToLowerInvariant() switch
        {
            "number" => query.OrderBy(si => si.InvoiceNumber),
            "number_desc" => query.OrderByDescending(si => si.InvoiceNumber),
            "date" => query.OrderBy(si => si.InvoiceDateUtc),
            "date_desc" => query.OrderByDescending(si => si.InvoiceDateUtc),
            "total" => query.OrderBy(si => si.TotalAmount),
            "total_desc" => query.OrderByDescending(si => si.TotalAmount),
            "status" => query.OrderBy(si => si.Status),
            "created" => query.OrderBy(si => si.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(si => si.CreatedAtUtc),
            _ => query.OrderByDescending(si => si.CreatedAtUtc),
        };

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var allVariantIds = items.SelectMany(si => si.Lines).Select(l => l.VariantId).Distinct().ToList();
        var allVariants = await LoadVariantDictAsync(allVariantIds, ct);

        var dtos = items.Select(si => MapToDto(
            si, si.Customer?.Name, si.Warehouse.Name,
            si.Cashier.Username, allVariants, null)).ToList();
        return (dtos, totalCount);
    }

    // ───── Update (Draft only) ─────

    public async Task<(InvoiceDto? Invoice, string? ErrorCode, string? ErrorDetail)> UpdateAsync(
        Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
    {
        var invoice = await _db.SalesInvoices
            .Include(si => si.Customer)
            .Include(si => si.Warehouse)
            .Include(si => si.Cashier)
            .FirstOrDefaultAsync(si => si.Id == id, ct);

        if (invoice is null)
            return (null, ErrorCodes.SalesInvoiceNotFound, "Sales invoice not found.");

        if (invoice.Status != SalesInvoiceStatus.Draft)
            return (null, ErrorCodes.SalesInvoiceAlreadyPosted, "Cannot modify a posted sales invoice.");

        if (request.WarehouseId.HasValue)
        {
            var warehouse = await _db.Warehouses
                .FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value && w.IsActive, ct);
            if (warehouse is null)
                return (null, ErrorCodes.WarehouseNotFound, "Warehouse not found or inactive.");
            invoice.WarehouseId = request.WarehouseId.Value;
            invoice.Warehouse = warehouse;
        }

        if (request.ClearCustomer)
        {
            invoice.CustomerId = null;
            invoice.Customer = null;
        }
        else if (request.CustomerId.HasValue)
        {
            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value, ct);
            if (customer is null)
                return (null, ErrorCodes.CustomerNotFound, "Customer not found.");
            invoice.CustomerId = request.CustomerId.Value;
            invoice.Customer = customer;
        }

        if (request.Notes is not null)
            invoice.Notes = request.Notes;

        // Replace lines if provided
        if (request.Lines is not null)
        {
            if (request.Lines.Count == 0)
                return (null, ErrorCodes.SalesInvoiceEmpty, "Sales invoice must have at least one line.");

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
                if (line.UnitPrice < 0)
                    return (null, ErrorCodes.ValidationFailed, "Line unit price cannot be negative.");
                if (line.DiscountAmount.HasValue && line.DiscountAmount.Value < 0)
                    return (null, ErrorCodes.ValidationFailed, "Line discount cannot be negative.");
            }

            // Delete existing lines (not tracked — avoids concurrency issues)
            await _db.SalesInvoiceLines
                .Where(l => l.SalesInvoiceId == invoice.Id)
                .ExecuteDeleteAsync(ct);

            decimal totalAmount = 0m;
            foreach (var line in request.Lines)
            {
                var discount = line.DiscountAmount ?? 0m;
                var lineTotal = (line.Quantity * line.UnitPrice) - discount;

                _db.SalesInvoiceLines.Add(new SalesInvoiceLine
                {
                    Id = Guid.NewGuid(),
                    SalesInvoiceId = invoice.Id,
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = discount,
                    LineTotal = lineTotal,
                });
                totalAmount += lineTotal;
            }
            invoice.TotalAmount = totalAmount;
        }

        await _db.SaveChangesAsync(ct);

        // Reload the full invoice for DTO mapping
        var result = await GetByIdAsync(id, ct);
        return (result, null, null);
    }

    // ───── Delete (Draft only) ─────

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> DeleteAsync(
        Guid id, CancellationToken ct = default)
    {
        var invoice = await _db.SalesInvoices.FindAsync([id], ct);
        if (invoice is null)
            return (false, ErrorCodes.SalesInvoiceNotFound, "Sales invoice not found.");

        if (invoice.Status != SalesInvoiceStatus.Draft)
            return (false, ErrorCodes.SalesInvoiceAlreadyPosted, "Cannot delete a posted sales invoice.");

        _db.SalesInvoices.Remove(invoice);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ───── Post (Atomic + Idempotent) ─────

    /// <summary>
    /// Posts a draft sales invoice atomically:
    /// 1. Claims the entity with an atomic UPDATE … WHERE Status = Draft (TOCTOU prevention)
    /// 2. Creates a SaleIssue stock movement (negative deltas for all lines)
    /// 3. Creates a CustomerReceivable placeholder if the invoice has a customer
    /// Idempotent: if already posted, returns the existing movement ID.
    /// Concurrent duplicates get 409 POST_CONCURRENCY_CONFLICT.
    /// </summary>
    public async Task<PostResult> PostAsync(Guid invoiceId, Guid userId, CancellationToken ct = default)
    {
        // ── Atomic concurrency gate ──
        // Single UPDATE … WHERE Status = Draft ensures only one caller can post.
        var claimed = await _db.SalesInvoices
            .Where(si => si.Id == invoiceId && si.Status == SalesInvoiceStatus.Draft)
            .ExecuteUpdateAsync(s => s
                .SetProperty(si => si.Status, SalesInvoiceStatus.Posted)
                .SetProperty(si => si.PostedAtUtc, DateTime.UtcNow), ct);

        if (claimed == 0)
        {
            var existing = await _db.SalesInvoices
                .AsNoTracking()
                .Where(si => si.Id == invoiceId)
                .Select(si => new { si.Status, si.StockMovementId })
                .FirstOrDefaultAsync(ct);

            if (existing is null)
                return new PostResult(false, null, ErrorCodes.SalesInvoiceNotFound, "Sales invoice not found.");

            // Already fully posted → idempotent success
            if (existing.Status == SalesInvoiceStatus.Posted && existing.StockMovementId.HasValue)
                return new PostResult(true, existing.StockMovementId, ErrorCodes.PostAlreadyPosted, "Already posted.");

            // Posted but StockMovementId null → another request is mid-posting
            return new PostResult(false, null, ErrorCodes.PostConcurrencyConflict,
                "Posting in progress by another request. Retry shortly.");
        }

        // ── Load entity for stock movement ──
        var invoice = await _db.SalesInvoices
            .Include(si => si.Lines)
            .FirstOrDefaultAsync(si => si.Id == invoiceId, ct);

        if (invoice!.Lines.Count == 0)
        {
            await RollbackSalesClaimAsync(invoiceId, ct);
            return new PostResult(false, null, ErrorCodes.SalesInvoiceEmpty, "Sales invoice has no lines.");
        }

        // ── Create SaleIssue stock movement (negative deltas = stock out) ──
        var stockLines = invoice.Lines.Select(line =>
            new StockService.PostMovementLineRequest(
                line.VariantId,
                invoice.WarehouseId,
                -line.Quantity,
                line.UnitPrice,
                $"Sale {invoice.InvoiceNumber}"
            )).ToList();

        var movementRequest = new StockService.PostMovementRequest(
            MovementType.SaleIssue,
            invoice.InvoiceNumber,
            $"Sales invoice {invoice.InvoiceNumber}",
            stockLines);

        var stockResult = await _stockService.PostAsync(movementRequest, userId, ct);
        if (!stockResult.Success)
        {
            await RollbackSalesClaimAsync(invoiceId, ct);
            return new PostResult(false, null, stockResult.ErrorCode, stockResult.ErrorDetail);
        }

        // ── Finalize: link movement + create receivable ──
        invoice.StockMovementId = stockResult.MovementId;

        if (invoice.CustomerId.HasValue)
        {
            _db.CustomerReceivables.Add(new CustomerReceivable
            {
                Id = Guid.NewGuid(),
                CustomerId = invoice.CustomerId.Value,
                SalesInvoiceId = invoice.Id,
                Amount = invoice.TotalAmount,
                IsPaid = false,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);

        // Create AR ledger entry (increases customer outstanding)
        if (invoice.CustomerId.HasValue)
        {
            await _accountingService.CreateInvoiceEntryAsync(
                PartyType.Customer, invoice.CustomerId.Value,
                invoice.TotalAmount, invoice.Id,
                $"Sales invoice {invoice.InvoiceNumber}",
                userId, ct);
        }

        return new PostResult(true, stockResult.MovementId, null, null);
    }

    /// <summary>Reverts the atomic claim if stock posting fails.</summary>
    private async Task RollbackSalesClaimAsync(Guid invoiceId, CancellationToken ct)
    {
        await _db.SalesInvoices
            .Where(si => si.Id == invoiceId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(si => si.Status, SalesInvoiceStatus.Draft)
                .SetProperty(si => si.PostedAtUtc, (DateTime?)null), ct);
    }

    // ───── Helpers ─────

    private static DateTime NormalizeInvoiceDateUtc(DateTime? invoiceDateUtc)
    {
        if (!invoiceDateUtc.HasValue)
            return DateTime.UtcNow;

        var value = invoiceDateUtc.Value;
        if (value.Kind == DateTimeKind.Utc)
            return value;

        // The create form uses a date picker, so midnight values represent a business date.
        if (value.TimeOfDay == TimeSpan.Zero)
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

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

    private async Task<InvoicePaymentTraceDto?> LoadPaymentTraceAsync(
        SalesInvoice invoice,
        CancellationToken ct)
    {
        if (!invoice.CustomerId.HasValue || invoice.Status != SalesInvoiceStatus.Posted)
            return null;

        var paymentEntries = await _db.LedgerEntries
            .Where(entry => entry.PartyType == PartyType.Customer
                            && entry.PartyId == invoice.CustomerId.Value
                            && entry.EntryType == LedgerEntryType.Payment
                            && entry.RelatedInvoiceId == invoice.Id)
            .OrderBy(entry => entry.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        if (paymentEntries.Count == 0)
            return new InvoicePaymentTraceDto(0m, invoice.TotalAmount, null, null, null, 0);

        var paymentIds = paymentEntries
            .Where(entry => entry.RelatedPaymentId.HasValue)
            .Select(entry => entry.RelatedPaymentId!.Value)
            .Distinct()
            .ToList();

        var payments = paymentIds.Count == 0
            ? []
            : await _db.Payments
                .Where(payment => paymentIds.Contains(payment.Id))
                .OrderBy(payment => payment.PaymentDateUtc)
                .AsNoTracking()
                .ToListAsync(ct);

        var paidAmount = payments.Count > 0
            ? payments.Sum(payment => payment.Amount)
            : paymentEntries.Sum(entry => -entry.Amount);

        var remainingAmount = Math.Max(invoice.TotalAmount - paidAmount, 0m);

        string? paymentMethod = null;
        string? walletName = null;
        if (payments.Count > 0)
        {
            var methods = payments
                .Select(payment => payment.Method.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (methods.Count == 1)
                paymentMethod = methods[0];

            var walletNames = payments
                .Select(payment => payment.WalletName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (walletNames.Count == 1)
                walletName = walletNames[0];
        }

        string? paymentReference = null;
        if (payments.Count > 0)
        {
            var references = payments
                .Select(payment => payment.Reference)
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => reference!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (references.Count == 1)
                paymentReference = references[0];
        }
        else
        {
            var references = paymentEntries
                .Select(entry => entry.Reference)
                .Where(reference => !string.IsNullOrWhiteSpace(reference) && !reference!.StartsWith("Payment ", StringComparison.OrdinalIgnoreCase))
                .Select(reference => reference!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (references.Count == 1)
                paymentReference = references[0];
        }

        return new InvoicePaymentTraceDto(
            paidAmount,
            remainingAmount,
            paymentMethod,
                walletName,
            paymentReference,
            paymentIds.Count > 0 ? paymentIds.Count : paymentEntries.Count);
    }

    private static InvoiceDto MapToDto(
        SalesInvoice invoice, string? customerName, string warehouseName,
        string cashierUsername, Dictionary<Guid, ProductVariant> variants,
        InvoicePaymentTraceDto? paymentTrace = null)
    {
        var lineDtos = invoice.Lines.Select(l =>
        {
            var variant = variants.GetValueOrDefault(l.VariantId);
            return new InvoiceLineDto(
                l.Id, l.VariantId,
                variant?.Sku ?? "UNKNOWN",
                variant?.Product?.Name,
                l.Quantity, l.UnitPrice,
                l.DiscountAmount, l.LineTotal);
        }).ToList();

        return new InvoiceDto(
            invoice.Id, invoice.InvoiceNumber,
            invoice.InvoiceDateUtc,
            invoice.CustomerId, customerName,
            invoice.WarehouseId, warehouseName,
            invoice.CashierUserId, cashierUsername,
            invoice.Notes, invoice.Status.ToString(),
            invoice.StockMovementId,
            invoice.TotalAmount,
            invoice.CreatedAtUtc, invoice.PostedAtUtc,
            lineDtos,
            paymentTrace);
    }
}

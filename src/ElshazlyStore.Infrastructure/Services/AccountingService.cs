using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Handles AR/AP ledger queries, balance computation, and payment creation.
/// Balances are always derived from ledger entries — never stored as mutable fields.
/// </summary>
public sealed class AccountingService
{
    private readonly AppDbContext _db;

    public AccountingService(AppDbContext db)
    {
        _db = db;
    }

    // ───── DTOs ─────

    public sealed record PartyBalanceDto(
        Guid PartyId,
        string PartyName,
        string PartyCode,
        PartyType PartyType,
        decimal TotalDebits,
        decimal TotalCredits,
        decimal Outstanding);

    public sealed record LedgerEntryDto(
        Guid Id,
        PartyType PartyType,
        Guid PartyId,
        string PartyName,
        string EntryType,
        decimal Amount,
        string? Reference,
        string? Notes,
        Guid? RelatedInvoiceId,
        Guid? RelatedPaymentId,
        DateTime CreatedAtUtc,
        Guid CreatedByUserId,
        string CreatedByUsername);

    public sealed record PaymentDto(
        Guid Id,
        PartyType PartyType,
        Guid PartyId,
        string PartyName,
        decimal Amount,
        string Method,
        string? WalletName,
        string? Reference,
        DateTime PaymentDateUtc,
        DateTime CreatedAtUtc,
        Guid CreatedByUserId,
        string CreatedByUsername);

    public sealed record CreatePaymentRequest(
        PartyType PartyType,
        Guid PartyId,
        decimal Amount,
        PaymentMethod Method,
        string? WalletName,
        string? Reference,
        Guid? RelatedInvoiceId,
        DateTime? PaymentDateUtc);

    public sealed record CreatePaymentResult(
        bool Success,
        PaymentDto? Payment,
        string? ErrorCode,
        string? ErrorDetail);

    // ───── Ledger Entry Creation (internal, called by other services) ─────

    /// <summary>
    /// Creates a ledger entry for an invoice posting.
    /// Positive amount = increases outstanding (receivable/payable).
    /// </summary>
    public async Task CreateInvoiceEntryAsync(
        PartyType partyType, Guid partyId, decimal amount,
        Guid relatedInvoiceId, string reference,
        Guid createdByUserId, CancellationToken ct = default)
    {
        var entry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            PartyType = partyType,
            PartyId = partyId,
            EntryType = LedgerEntryType.Invoice,
            Amount = amount,
            Reference = reference,
            RelatedInvoiceId = relatedInvoiceId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };

        _db.LedgerEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates an opening balance ledger entry.
    /// </summary>
    public async Task CreateOpeningBalanceEntryAsync(
        PartyType partyType, Guid partyId, decimal amount,
        string? reference, Guid createdByUserId, CancellationToken ct = default)
    {
        var entry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            PartyType = partyType,
            PartyId = partyId,
            EntryType = LedgerEntryType.OpeningBalance,
            Amount = amount,
            Reference = reference,
            Notes = "Opening balance",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };

        _db.LedgerEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    // ───── Payments ─────

    /// <summary>
    /// Creates a payment and a corresponding negative ledger entry.
    /// By default, overpayment is disallowed.
    /// </summary>
    public async Task<CreatePaymentResult> CreatePaymentAsync(
        CreatePaymentRequest request, Guid createdByUserId,
        bool allowOverpay = false, CancellationToken ct = default)
    {
        // Validate party type and find party
        if (request.Amount <= 0)
            return new CreatePaymentResult(false, null, ErrorCodes.ValidationFailed,
                "Payment amount must be positive.");

        // Validate EWallet requires walletName
        if (request.Method == PaymentMethod.EWallet &&
            string.IsNullOrWhiteSpace(request.WalletName))
            return new CreatePaymentResult(false, null, ErrorCodes.WalletNameRequired,
                "Wallet name is required when payment method is EWallet.");

        // Validate party exists
        string partyName;
        string partyCode;
        if (request.PartyType == PartyType.Customer)
        {
            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.Id == request.PartyId, ct);
            if (customer is null)
                return new CreatePaymentResult(false, null, ErrorCodes.PartyNotFound,
                    "Customer not found.");
            partyName = customer.Name;
            partyCode = customer.Code;
        }
        else if (request.PartyType == PartyType.Supplier)
        {
            var supplier = await _db.Suppliers
                .FirstOrDefaultAsync(s => s.Id == request.PartyId, ct);
            if (supplier is null)
                return new CreatePaymentResult(false, null, ErrorCodes.PartyNotFound,
                    "Supplier not found.");
            partyName = supplier.Name;
            partyCode = supplier.Code;
        }
        else
        {
            return new CreatePaymentResult(false, null, ErrorCodes.InvalidPartyType,
                "Invalid party type. Must be Customer or Supplier.");
        }

        // Check outstanding balance (disallow overpay by default)
        if (!allowOverpay)
        {
            var outstanding = await ComputeOutstandingAsync(request.PartyType, request.PartyId, ct);
            if (request.Amount > outstanding)
                return new CreatePaymentResult(false, null, ErrorCodes.OverpaymentNotAllowed,
                    $"Payment amount {request.Amount} exceeds outstanding balance {outstanding}.");
        }

        if (request.RelatedInvoiceId.HasValue)
        {
            if (request.PartyType != PartyType.Customer)
                return new CreatePaymentResult(false, null, ErrorCodes.ValidationFailed,
                    "Related invoice links are supported for customer payments only.");

            var invoice = await _db.SalesInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(si => si.Id == request.RelatedInvoiceId.Value, ct);

            if (invoice is null)
                return new CreatePaymentResult(false, null, ErrorCodes.SalesInvoiceNotFound,
                    "Sales invoice not found.");

            if (invoice.Status != SalesInvoiceStatus.Posted)
                return new CreatePaymentResult(false, null, ErrorCodes.ValidationFailed,
                    "Related sales invoice must be posted before linking a payment.");

            if (invoice.CustomerId != request.PartyId)
                return new CreatePaymentResult(false, null, ErrorCodes.ValidationFailed,
                    "Related sales invoice does not belong to the specified customer.");
        }

        // Create payment record
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            PartyType = request.PartyType,
            PartyId = request.PartyId,
            Amount = request.Amount,
            Method = request.Method,
            WalletName = request.Method == PaymentMethod.EWallet ? request.WalletName : null,
            Reference = request.Reference,
            PaymentDateUtc = request.PaymentDateUtc ?? DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
        _db.Payments.Add(payment);

        // Create negative ledger entry (reduces outstanding)
        var ledgerEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            PartyType = request.PartyType,
            PartyId = request.PartyId,
            EntryType = LedgerEntryType.Payment,
            Amount = -request.Amount, // negative = reduces outstanding
            Reference = request.Reference ?? $"Payment {payment.Id:N}",
            Notes = $"Payment via {request.Method}" +
                    (request.Method == PaymentMethod.EWallet ? $" ({request.WalletName})" : ""),
                RelatedInvoiceId = request.RelatedInvoiceId,
            RelatedPaymentId = payment.Id,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
        _db.LedgerEntries.Add(ledgerEntry);

        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FindAsync([createdByUserId], ct);
        var dto = MapPaymentToDto(payment, partyName, user!.Username);

        return new CreatePaymentResult(true, dto, null, null);
    }

    // ───── Queries ─────

    /// <summary>
    /// Compute outstanding balance for a party — DB-side SUM.
    /// Uses double cast for SQLite compat (decimal Sum not supported).
    /// </summary>
    public async Task<decimal> ComputeOutstandingAsync(
        PartyType partyType, Guid partyId, CancellationToken ct = default)
    {
        return (decimal)await _db.LedgerEntries
            .Where(e => e.PartyType == partyType && e.PartyId == partyId)
            .SumAsync(e => (double)e.Amount, ct);
    }

    /// <summary>
    /// Get balances for all parties of a given type — DB-side aggregation.
    /// Uses double casts for SQLite compatibility.
    /// </summary>
    public async Task<(List<PartyBalanceDto> Items, int TotalCount)> GetBalancesAsync(
        PartyType partyType, string? search, int page, int pageSize,
        bool includeTotal = true, CancellationToken ct = default)
    {
        // DB-side GroupBy aggregation (double for SQLite compat)
        var balances = _db.LedgerEntries
            .Where(e => e.PartyType == partyType)
            .GroupBy(e => e.PartyId)
            .Select(g => new
            {
                PartyId = g.Key,
                TotalDebits = g.Sum(e => e.Amount > 0 ? (double)e.Amount : 0.0),
                TotalCredits = g.Sum(e => e.Amount < 0 ? -(double)e.Amount : 0.0),
                Outstanding = g.Sum(e => (double)e.Amount),
            });

        if (partyType == PartyType.Customer)
        {
            var query = balances
                .Join(_db.Customers, b => b.PartyId, c => c.Id, (b, c) => new
                {
                    b.PartyId,
                    PartyName = c.Name,
                    PartyCode = c.Code,
                    b.TotalDebits,
                    b.TotalCredits,
                    b.Outstanding,
                });

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmed = search.Trim();
                if (_db.Database.IsNpgsql())
                {
                    var p = $"%{trimmed}%";
                    query = query.Where(r =>
                        EF.Functions.ILike(r.PartyName, p) ||
                        EF.Functions.ILike(r.PartyCode, p));
                }
                else
                {
                    var p = $"%{trimmed.ToLowerInvariant()}%";
                    query = query.Where(r =>
                        EF.Functions.Like(r.PartyName.ToLower(), p) ||
                        EF.Functions.Like(r.PartyCode.ToLower(), p));
                }
            }

            var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
            var items = await query
                .OrderByDescending(r => r.Outstanding)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new PartyBalanceDto(
                    r.PartyId, r.PartyName, r.PartyCode, partyType,
                    (decimal)r.TotalDebits, (decimal)r.TotalCredits, (decimal)r.Outstanding))
                .ToListAsync(ct);

            return (items, totalCount);
        }
        else
        {
            var query = balances
                .Join(_db.Suppliers, b => b.PartyId, s => s.Id, (b, s) => new
                {
                    b.PartyId,
                    PartyName = s.Name,
                    PartyCode = s.Code,
                    b.TotalDebits,
                    b.TotalCredits,
                    b.Outstanding,
                });

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmed = search.Trim();
                if (_db.Database.IsNpgsql())
                {
                    var p = $"%{trimmed}%";
                    query = query.Where(r =>
                        EF.Functions.ILike(r.PartyName, p) ||
                        EF.Functions.ILike(r.PartyCode, p));
                }
                else
                {
                    var p = $"%{trimmed.ToLowerInvariant()}%";
                    query = query.Where(r =>
                        EF.Functions.Like(r.PartyName.ToLower(), p) ||
                        EF.Functions.Like(r.PartyCode.ToLower(), p));
                }
            }

            var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
            var items = await query
                .OrderByDescending(r => r.Outstanding)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new PartyBalanceDto(
                    r.PartyId, r.PartyName, r.PartyCode, partyType,
                    (decimal)r.TotalDebits, (decimal)r.TotalCredits, (decimal)r.Outstanding))
                .ToListAsync(ct);

            return (items, totalCount);
        }
    }

    /// <summary>
    /// Get ledger entries for a specific party.
    /// </summary>
    public async Task<(List<LedgerEntryDto> Items, int TotalCount)> GetLedgerAsync(
        PartyType partyType, Guid partyId, int page, int pageSize,
        bool includeTotal = true, CancellationToken ct = default)
    {
        var query = _db.LedgerEntries
            .Where(e => e.PartyType == partyType && e.PartyId == partyId)
            .Include(e => e.CreatedBy)
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Get party name
        var partyName = await GetPartyNameAsync(partyType, partyId, ct);

        var dtos = items.Select(e => new LedgerEntryDto(
            e.Id, e.PartyType, e.PartyId, partyName,
            e.EntryType.ToString(), e.Amount,
            e.Reference, e.Notes,
            e.RelatedInvoiceId, e.RelatedPaymentId,
            e.CreatedAtUtc, e.CreatedByUserId,
            e.CreatedBy.Username)).ToList();

        return (dtos, totalCount);
    }

    /// <summary>
    /// Get payments for a specific party or all parties.
    /// </summary>
    public async Task<(List<PaymentDto> Items, int TotalCount)> GetPaymentsAsync(
        PartyType? partyType, Guid? partyId, string? search,
        int page, int pageSize, bool includeTotal = true,
        CancellationToken ct = default)
    {
        var query = _db.Payments
            .Include(p => p.CreatedBy)
            .AsNoTracking()
            .AsQueryable();

        if (partyType.HasValue)
            query = query.Where(p => p.PartyType == partyType.Value);

        if (partyId.HasValue)
            query = query.Where(p => p.PartyId == partyId.Value);

        query = query.ApplySearch(_db.Database, search,
            p => p.Reference,
            p => p.WalletName);

        query = query.OrderByDescending(p => p.PaymentDateUtc);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Load party names
        var customerIds = items.Where(p => p.PartyType == PartyType.Customer).Select(p => p.PartyId).Distinct().ToList();
        var supplierIds = items.Where(p => p.PartyType == PartyType.Supplier).Select(p => p.PartyId).Distinct().ToList();

        var customers = customerIds.Count > 0
            ? await _db.Customers.Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct)
            : new Dictionary<Guid, Customer>();

        var suppliers = supplierIds.Count > 0
            ? await _db.Suppliers.Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct)
            : new Dictionary<Guid, Supplier>();

        var dtos = items.Select(p =>
        {
            var name = p.PartyType == PartyType.Customer
                ? customers.GetValueOrDefault(p.PartyId)?.Name ?? "UNKNOWN"
                : suppliers.GetValueOrDefault(p.PartyId)?.Name ?? "UNKNOWN";
            return MapPaymentToDto(p, name, p.CreatedBy.Username);
        }).ToList();

        return (dtos, totalCount);
    }

    /// <summary>
    /// Get a single payment by ID.
    /// </summary>
    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(p => p.CreatedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (payment is null) return null;

        var partyName = await GetPartyNameAsync(payment.PartyType, payment.PartyId, ct);
        return MapPaymentToDto(payment, partyName, payment.CreatedBy.Username);
    }

    // ───── Helpers ─────

    private async Task<string> GetPartyNameAsync(PartyType partyType, Guid partyId, CancellationToken ct)
    {
        if (partyType == PartyType.Customer)
        {
            var customer = await _db.Customers.FindAsync([partyId], ct);
            return customer?.Name ?? "UNKNOWN";
        }
        else
        {
            var supplier = await _db.Suppliers.FindAsync([partyId], ct);
            return supplier?.Name ?? "UNKNOWN";
        }
    }

    private static PaymentDto MapPaymentToDto(Payment payment, string partyName, string username)
    {
        return new PaymentDto(
            payment.Id,
            payment.PartyType,
            payment.PartyId,
            partyName,
            payment.Amount,
            payment.Method.ToString(),
            payment.WalletName,
            payment.Reference,
            payment.PaymentDateUtc,
            payment.CreatedAtUtc,
            payment.CreatedByUserId,
            username);
    }
}

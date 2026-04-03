using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Server-side aggregation queries for dashboard KPIs.
/// All monetary/quantity aggregations use client-side evaluation (ToList then Sum)
/// to remain compatible with both PostgreSQL and SQLite (tests).
///
/// KPI Definitions (Phase RET 4):
/// ─────────────────────────────
/// • TotalSales        = sum of posted sales invoice amounts in date range.
/// • TotalSalesReturns = sum of posted sales return amounts in date range.
/// • NetSales          = TotalSales − TotalSalesReturns.
/// • InvoiceCount      = number of posted sales invoices.
/// • ReturnCount       = number of posted sales returns.
/// • AverageTicket     = TotalSales / InvoiceCount (gross, per invoice).
/// • NetAverageTicket  = NetSales / InvoiceCount.
/// • Top Products (Net)= variant aggregation: gross qty/revenue minus returned qty/revenue.
/// • DispositionLoss   = total value of posted inventory dispositions in date range.
/// </summary>
public sealed class DashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    // ───── DTOs ─────

    public sealed record DashboardSummaryDto(
        SalesSummaryDto Sales,
        List<TopProductDto> TopProductsByQuantity,
        List<TopProductDto> TopProductsByRevenue,
        List<LowStockAlertDto> LowStockAlerts,
        List<CashierPerformanceDto> CashierPerformance,
        decimal DispositionLoss);

    public sealed record SalesSummaryDto(
        decimal TotalSales,
        int InvoiceCount,
        decimal AverageTicket,
        decimal TotalSalesReturns,
        int ReturnCount,
        decimal NetSales,
        decimal NetAverageTicket);

    public sealed record TopProductDto(
        Guid ProductId,
        string ProductName,
        Guid VariantId,
        string Sku,
        string? Color,
        string? Size,
        decimal TotalQuantity,
        decimal TotalRevenue,
        decimal ReturnedQuantity,
        decimal ReturnedRevenue,
        decimal NetQuantity,
        decimal NetRevenue);

    public sealed record LowStockAlertDto(
        Guid VariantId,
        string Sku,
        string ProductName,
        string? Color,
        string? Size,
        Guid WarehouseId,
        string WarehouseCode,
        string WarehouseName,
        decimal CurrentStock,
        decimal Threshold);

    public sealed record CashierPerformanceDto(
        Guid CashierUserId,
        string CashierUsername,
        int InvoiceCount,
        decimal TotalSales,
        decimal AverageTicket);

    // ───── Main Query ─────

    /// <summary>
    /// Build a full dashboard summary for a date range.
    /// Only considers Posted invoices.
    /// </summary>
    public async Task<DashboardSummaryDto> GetSummaryAsync(
        DateTime from, DateTime to,
        int topN = 10,
        decimal defaultLowStockThreshold = 5m,
        CancellationToken ct = default)
    {
        var sales = await GetSalesSummaryAsync(from, to, ct);
        var topByQty = await GetTopProductsAsync(from, to, topN, orderByRevenue: false, ct);
        var topByRev = await GetTopProductsAsync(from, to, topN, orderByRevenue: true, ct);
        var lowStock = await GetLowStockAlertsAsync(defaultLowStockThreshold, ct);
        var cashiers = await GetCashierPerformanceAsync(from, to, ct);
        var dispLoss = await GetDispositionLossAsync(from, to, ct);

        return new DashboardSummaryDto(sales, topByQty, topByRev, lowStock, cashiers, dispLoss);
    }

    // ───── Sales Summary ─────

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        // Gross sales
        var invoices = await _db.SalesInvoices
            .AsNoTracking()
            .Where(i => i.Status == SalesInvoiceStatus.Posted
                     && i.PostedAtUtc >= from
                     && i.PostedAtUtc <= to)
            .Select(i => i.TotalAmount)
            .ToListAsync(ct);

        var totalSales = invoices.Sum();
        var invoiceCount = invoices.Count;
        var avgTicket = invoiceCount > 0 ? totalSales / invoiceCount : 0m;

        // Sales returns (posted, within date range)
        var returns = await _db.SalesReturns
            .AsNoTracking()
            .Where(r => r.Status == SalesReturnStatus.Posted
                     && r.PostedAtUtc >= from
                     && r.PostedAtUtc <= to)
            .Select(r => r.TotalAmount)
            .ToListAsync(ct);

        var totalReturns = returns.Sum();
        var returnCount = returns.Count;
        var netSales = totalSales - totalReturns;
        var netAvgTicket = invoiceCount > 0 ? netSales / invoiceCount : 0m;

        return new SalesSummaryDto(
            totalSales, invoiceCount, avgTicket,
            totalReturns, returnCount, netSales, netAvgTicket);
    }

    // ───── Top Products ─────

    public async Task<List<TopProductDto>> GetTopProductsAsync(
        DateTime from, DateTime to, int topN,
        bool orderByRevenue, CancellationToken ct = default)
    {
        // Load posted invoice line data within range, then aggregate client-side
        var lines = await _db.SalesInvoiceLines
            .AsNoTracking()
            .Include(l => l.Variant).ThenInclude(v => v.Product)
            .Where(l => l.SalesInvoice.Status == SalesInvoiceStatus.Posted
                     && l.SalesInvoice.PostedAtUtc >= from
                     && l.SalesInvoice.PostedAtUtc <= to)
            .Select(l => new
            {
                l.VariantId,
                l.Variant.ProductId,
                ProductName = l.Variant.Product.Name,
                l.Variant.Sku,
                l.Variant.Color,
                l.Variant.Size,
                l.Quantity,
                l.LineTotal,
            })
            .ToListAsync(ct);

        // Load posted return line data within range
        var returnLines = await _db.SalesReturnLines
            .AsNoTracking()
            .Include(l => l.Variant).ThenInclude(v => v.Product)
            .Where(l => l.SalesReturn.Status == SalesReturnStatus.Posted
                     && l.SalesReturn.PostedAtUtc >= from
                     && l.SalesReturn.PostedAtUtc <= to)
            .Select(l => new
            {
                l.VariantId,
                l.Variant.ProductId,
                ProductName = l.Variant.Product.Name,
                l.Variant.Sku,
                l.Variant.Color,
                l.Variant.Size,
                l.Quantity,
                l.LineTotal,
            })
            .ToListAsync(ct);

        // Build return lookup by VariantId
        var returnsByVariant = returnLines
            .GroupBy(l => l.VariantId)
            .ToDictionary(
                g => g.Key,
                g => (Qty: g.Sum(x => x.Quantity), Revenue: g.Sum(x => x.LineTotal)));

        var grouped = lines
            .GroupBy(l => l.VariantId)
            .Select(g =>
            {
                var first = g.First();
                var grossQty = g.Sum(x => x.Quantity);
                var grossRev = g.Sum(x => x.LineTotal);
                var (retQty, retRev) = returnsByVariant.TryGetValue(g.Key, out var r) ? r : (0m, 0m);
                return new TopProductDto(
                    first.ProductId,
                    first.ProductName,
                    first.VariantId,
                    first.Sku,
                    first.Color,
                    first.Size,
                    grossQty,
                    grossRev,
                    retQty,
                    retRev,
                    grossQty - retQty,
                    grossRev - retRev);
            });

        var ordered = orderByRevenue
            ? grouped.OrderByDescending(p => p.NetRevenue)
            : grouped.OrderByDescending(p => p.NetQuantity);

        return ordered.Take(topN).ToList();
    }

    // ───── Low Stock Alerts ─────

    public async Task<List<LowStockAlertDto>> GetLowStockAlertsAsync(
        decimal defaultThreshold, CancellationToken ct = default)
    {
        // Load all stock balances with variant and warehouse info
        var balances = await _db.StockBalances
            .AsNoTracking()
            .Include(b => b.Variant).ThenInclude(v => v.Product)
            .Include(b => b.Warehouse)
            .Where(b => b.Variant.IsActive && b.Warehouse.IsActive)
            .Select(b => new
            {
                b.VariantId,
                b.Variant.Sku,
                ProductName = b.Variant.Product.Name,
                b.Variant.Color,
                b.Variant.Size,
                b.Variant.LowStockThreshold,
                b.WarehouseId,
                WarehouseCode = b.Warehouse.Code,
                WarehouseName = b.Warehouse.Name,
                b.Quantity,
            })
            .ToListAsync(ct);

        return balances
            .Where(b =>
            {
                var threshold = b.LowStockThreshold ?? defaultThreshold;
                return b.Quantity <= threshold;
            })
            .Select(b => new LowStockAlertDto(
                b.VariantId, b.Sku, b.ProductName,
                b.Color, b.Size,
                b.WarehouseId, b.WarehouseCode, b.WarehouseName,
                b.Quantity,
                b.LowStockThreshold ?? defaultThreshold))
            .OrderBy(a => a.CurrentStock)
            .ToList();
    }

    // ───── Cashier Performance ─────

    public async Task<List<CashierPerformanceDto>> GetCashierPerformanceAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var invoices = await _db.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Cashier)
            .Where(i => i.Status == SalesInvoiceStatus.Posted
                     && i.PostedAtUtc >= from
                     && i.PostedAtUtc <= to)
            .Select(i => new
            {
                i.CashierUserId,
                CashierUsername = i.Cashier.Username,
                i.TotalAmount,
            })
            .ToListAsync(ct);

        return invoices
            .GroupBy(i => i.CashierUserId)
            .Select(g =>
            {
                var first = g.First();
                var total = g.Sum(x => x.TotalAmount);
                var count = g.Count();
                return new CashierPerformanceDto(
                    first.CashierUserId,
                    first.CashierUsername,
                    count,
                    total,
                    count > 0 ? total / count : 0m);
            })
            .OrderByDescending(c => c.TotalSales)
            .ToList();
    }

    // ───── Disposition Loss ─────

    /// <summary>
    /// Total disposed quantity from posted inventory dispositions within the date range.
    /// Dispositions do not carry monetary values — returns the total quantity disposed.
    /// </summary>
    public async Task<decimal> GetDispositionLossAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var quantities = await _db.InventoryDispositionLines
            .AsNoTracking()
            .Where(l => l.InventoryDisposition.Status == DispositionStatus.Posted
                     && l.InventoryDisposition.PostedAtUtc >= from
                     && l.InventoryDisposition.PostedAtUtc <= to)
            .Select(l => l.Quantity)
            .ToListAsync(ct);

        return quantities.Sum();
    }
}

using ElshazlyStore.Domain.Common;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Dashboard KPI endpoints for BI / analytics.
/// </summary>
public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        var dashboard = group.MapGroup("/dashboard").WithTags("Dashboard");

        dashboard.MapGet("/summary", GetSummaryAsync)
            .RequireAuthorization($"Permission:{Permissions.DashboardRead}");

        dashboard.MapGet("/sales", GetSalesAsync)
            .RequireAuthorization($"Permission:{Permissions.DashboardRead}");

        dashboard.MapGet("/top-products", GetTopProductsAsync)
            .RequireAuthorization($"Permission:{Permissions.DashboardRead}");

        dashboard.MapGet("/low-stock", GetLowStockAsync)
            .RequireAuthorization($"Permission:{Permissions.DashboardRead}");

        dashboard.MapGet("/cashier-performance", GetCashierPerformanceAsync)
            .RequireAuthorization($"Permission:{Permissions.DashboardRead}");

        return group;
    }

    /// <summary>
    /// Full dashboard summary: sales KPIs, top products, low stock, cashier performance.
    /// </summary>
    private static async Task<IResult> GetSummaryAsync(
        DashboardService dashboardService,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int topN = 10,
        [FromQuery] decimal lowStockThreshold = 5m,
        CancellationToken ct = default)
    {
        var (f, t) = NormalizeDateRange(from, to);
        if (topN < 1) topN = 1;
        if (topN > 100) topN = 100;

        var result = await dashboardService.GetSummaryAsync(f, t, topN, lowStockThreshold, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Sales summary only: total sales, invoice count, avg ticket.
    /// </summary>
    private static async Task<IResult> GetSalesAsync(
        DashboardService dashboardService,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var (f, t) = NormalizeDateRange(from, to);
        var result = await dashboardService.GetSalesSummaryAsync(f, t, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Top products/variants by quantity or revenue.
    /// </summary>
    private static async Task<IResult> GetTopProductsAsync(
        DashboardService dashboardService,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int topN = 10,
        [FromQuery] string orderBy = "quantity",
        CancellationToken ct = default)
    {
        var (f, t) = NormalizeDateRange(from, to);
        if (topN < 1) topN = 1;
        if (topN > 100) topN = 100;

        var byRevenue = orderBy.Equals("revenue", StringComparison.OrdinalIgnoreCase);
        var result = await dashboardService.GetTopProductsAsync(f, t, topN, byRevenue, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Low stock alerts for active variants below threshold.
    /// </summary>
    private static async Task<IResult> GetLowStockAsync(
        DashboardService dashboardService,
        [FromQuery] decimal threshold = 5m,
        CancellationToken ct = default)
    {
        var result = await dashboardService.GetLowStockAlertsAsync(threshold, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Cashier performance: invoice count, total sales, avg ticket per cashier.
    /// </summary>
    private static async Task<IResult> GetCashierPerformanceAsync(
        DashboardService dashboardService,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var (f, t) = NormalizeDateRange(from, to);
        var result = await dashboardService.GetCashierPerformanceAsync(f, t, ct);
        return Results.Ok(result);
    }

    /// <summary>
    /// Defaults: from = start of current month, to = now.
    /// </summary>
    private static (DateTime From, DateTime To) NormalizeDateRange(DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var f = from ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var t = to ?? now;
        return (f, t);
    }
}

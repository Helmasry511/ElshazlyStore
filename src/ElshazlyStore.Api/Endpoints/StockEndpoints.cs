using ElshazlyStore.Domain.Common;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Read-only stock endpoints: balances and ledger.
/// </summary>
public static class StockEndpoints
{
    public static RouteGroupBuilder MapStockEndpoints(this RouteGroupBuilder group)
    {
        var stock = group.MapGroup("/stock").WithTags("Stock");

        stock.MapGet("/balances", BalancesAsync)
            .RequireAuthorization($"Permission:{Permissions.StockRead}");
        stock.MapGet("/ledger", LedgerAsync)
            .RequireAuthorization($"Permission:{Permissions.StockRead}");

        return group;
    }

    private static async Task<IResult> BalancesAsync(
        StockService stockService,
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sort = "product",
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var (items, totalCount) = await stockService.GetBalancesAsync(warehouseId, q, page, pageSize, sort, includeTotal, ct);
        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    private static async Task<IResult> LedgerAsync(
        StockService stockService,
        [FromQuery] Guid? variantId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var (items, totalCount) = await stockService.GetLedgerAsync(variantId, warehouseId, from, to, page, pageSize, includeTotal, ct);
        return Results.Ok(new { items, totalCount, page, pageSize });
    }
}

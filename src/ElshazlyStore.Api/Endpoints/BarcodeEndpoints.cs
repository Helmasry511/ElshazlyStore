using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Barcode lookup: resolve barcode → product + variant info.
/// Results are cached with short TTL for POS speed.
/// </summary>
public static class BarcodeEndpoints
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public static RouteGroupBuilder MapBarcodeEndpoints(this RouteGroupBuilder group)
    {
        var barcodes = group.MapGroup("/barcodes").WithTags("Barcodes");

        barcodes.MapGet("/{barcode}", LookupAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsRead}");

        return group;
    }

    private static async Task<IResult> LookupAsync(string barcode, AppDbContext db, IMemoryCache cache)
    {
        var cacheKey = $"barcode:{barcode}";
        if (cache.TryGetValue(cacheKey, out BarcodeLookupResult? cached))
            return Results.Ok(cached);

        var reservation = await db.BarcodeReservations
            .AsNoTracking()
            .Include(b => b.Variant!)
                .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(b => b.Barcode == barcode);

        if (reservation is null)
            return Results.Problem(detail: "Barcode not found.", title: ErrorCodes.NotFound,
                statusCode: 404, type: "https://elshazlystore.local/errors/not_found");

        if (reservation.Status == BarcodeStatus.Retired)
            return Results.Problem(detail: "Barcode has been retired.", title: ErrorCodes.BarcodeRetired,
                statusCode: 410, type: "https://elshazlystore.local/errors/barcode_retired");

        if (reservation.Variant is null)
            return Results.Problem(detail: "Barcode is reserved but not yet assigned.",
                title: ErrorCodes.NotFound, statusCode: 404,
                type: "https://elshazlystore.local/errors/not_found");

        var v = reservation.Variant;
        var result = new BarcodeLookupResult(
            reservation.Barcode,
            reservation.Status.ToString(),
            v.Id, v.Sku, v.Color, v.Size, v.RetailPrice, v.WholesalePrice, v.IsActive,
            v.Product.Id, v.Product.Name, v.Product.Category);

        cache.Set(cacheKey, result, CacheTtl);
        return Results.Ok(result);
    }

    public sealed record BarcodeLookupResult(
        string Barcode, string Status,
        Guid VariantId, string Sku, string? Color, string? Size,
        decimal? RetailPrice, decimal? WholesalePrice, bool IsActive,
        Guid ProductId, string ProductName, string? ProductCategory);
}

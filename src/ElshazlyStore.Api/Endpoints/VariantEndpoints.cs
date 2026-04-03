using System.Text;
using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Variant CRUD with barcode assignment and paging/search.
/// </summary>
public static class VariantEndpoints
{
    public static RouteGroupBuilder MapVariantEndpoints(this RouteGroupBuilder group)
    {
        var variants = group.MapGroup("/variants").WithTags("Variants");

        variants.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsRead}");
        variants.MapGet("/by-sku/{sku}", GetBySkuAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsRead}");
        variants.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsRead}");
        variants.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsWrite}");
        variants.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsWrite}");
        variants.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsWrite}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        AppDbContext db,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = "sku",
        [FromQuery] bool includeTotal = true)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        IQueryable<ProductVariant> query = db.ProductVariants
            .Include(v => v.Product)
                .ThenInclude(p => p.DefaultWarehouse)
            .Include(v => v.BarcodeReservation);

        query = query.ApplySearch(db.Database, q,
            v => v.Sku,
            v => v.Product.Name,
            v => v.Color,
            v => v.Size,
            v => v.BarcodeReservation!.Barcode);

        query = sort?.ToLowerInvariant() switch
        {
            "sku" => query.OrderBy(v => v.Sku),
            "sku_desc" => query.OrderByDescending(v => v.Sku),
            "product" => query.OrderBy(v => v.Product.Name),
            "created" => query.OrderBy(v => v.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(v => v.CreatedAtUtc),
            _ => query.OrderBy(v => v.Sku),
        };

        var totalCount = includeTotal ? await query.CountAsync() : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VariantListDto(
                v.Id, v.ProductId, v.Product.Name, v.Sku, v.Color, v.Size,
                v.RetailPrice, v.WholesalePrice, v.IsActive,
                v.BarcodeReservation != null ? v.BarcodeReservation.Barcode : null,
                v.Product.DefaultWarehouseId,
                v.Product.DefaultWarehouse != null ? v.Product.DefaultWarehouse.Name : null))
            .ToListAsync();

        return Results.Ok(new PagedResult<VariantListDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db)
    {
        var variant = await db.ProductVariants
            .Include(v => v.Product)
                .ThenInclude(p => p.DefaultWarehouse)
            .Include(v => v.BarcodeReservation)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (variant is null) return Problem(404, ErrorCodes.NotFound, "Variant not found.");

        return Results.Ok(new VariantListDto(
            variant.Id, variant.ProductId, variant.Product.Name, variant.Sku,
            variant.Color, variant.Size, variant.RetailPrice, variant.WholesalePrice,
            variant.IsActive, variant.BarcodeReservation?.Barcode,
            variant.Product.DefaultWarehouseId,
            variant.Product.DefaultWarehouse?.Name));
    }

    /// <summary>
    /// Lookup a variant by its SKU (exact match, case-sensitive).
    /// Works for both server-generated and manually-assigned SKUs.
    /// </summary>
    private static async Task<IResult> GetBySkuAsync(string sku, AppDbContext db)
    {
        var variant = await db.ProductVariants
            .Include(v => v.Product)
                .ThenInclude(p => p.DefaultWarehouse)
            .Include(v => v.BarcodeReservation)
            .FirstOrDefaultAsync(v => v.Sku == sku);

        if (variant is null) return Problem(404, ErrorCodes.NotFound, $"Variant with SKU '{sku}' not found.");

        return Results.Ok(new VariantListDto(
            variant.Id, variant.ProductId, variant.Product.Name, variant.Sku,
            variant.Color, variant.Size, variant.RetailPrice, variant.WholesalePrice,
            variant.IsActive, variant.BarcodeReservation?.Barcode,
            variant.Product.DefaultWarehouseId,
            variant.Product.DefaultWarehouse?.Name));
    }

    /// <summary>Max retry attempts for identifier generation on unique-constraint violation.</summary>
    private const int MaxRetries = 5;

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateVariantRequest req, AppDbContext db)
    {
        var product = await db.Products
            .Include(p => p.DefaultWarehouse)
            .FirstOrDefaultAsync(p => p.Id == req.ProductId);
        if (product is null)
            return Problem(404, ErrorCodes.NotFound, "Parent product not found.");

        // Resolve SKU: use client value if provided, otherwise generate
        var skuProvided = !string.IsNullOrWhiteSpace(req.Sku);
        var sku = skuProvided ? req.Sku! : await IdentifierGenerator.GenerateSkuAsync(db);

        // If client supplied a SKU, validate uniqueness eagerly
        if (skuProvided && await db.ProductVariants.AnyAsync(v => v.Sku == sku))
            return Problem(409, ErrorCodes.Conflict, $"SKU '{sku}' already exists.");

        // Resolve Barcode: use client value if provided, otherwise generate
        var barcodeProvided = !string.IsNullOrWhiteSpace(req.Barcode);
        var barcodeValue = barcodeProvided ? req.Barcode! : IdentifierGenerator.GenerateBarcode();

        // If client supplied a barcode, validate uniqueness eagerly
        if (barcodeProvided)
        {
            var existingBarcode = await db.BarcodeReservations
                .FirstOrDefaultAsync(b => b.Barcode == barcodeValue);

            if (existingBarcode is not null)
            {
                var code = existingBarcode.Status == BarcodeStatus.Retired
                    ? ErrorCodes.BarcodeRetired
                    : ErrorCodes.BarcodeAlreadyExists;
                return Problem(409, code, $"Barcode '{barcodeValue}' is already taken.");
            }
        }

        // Retry loop for concurrency-safe identifier generation
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var variantId = Guid.NewGuid();
            var variant = new ProductVariant
            {
                Id = variantId,
                ProductId = req.ProductId,
                Sku = sku,
                Color = req.Color,
                Size = req.Size,
                RetailPrice = req.RetailPrice,
                WholesalePrice = req.WholesalePrice,
                LowStockThreshold = req.LowStockThreshold,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            };

            var barcode = new BarcodeReservation
            {
                Id = Guid.NewGuid(),
                Barcode = barcodeValue,
                ReservedAtUtc = DateTime.UtcNow,
                VariantId = variantId,
                Status = BarcodeStatus.Assigned,
            };

            db.ProductVariants.Add(variant);
            db.BarcodeReservations.Add(barcode);

            try
            {
                await db.SaveChangesAsync();

                return Results.Created($"/api/v1/variants/{variant.Id}",
                    new VariantListDto(variant.Id, variant.ProductId, product.Name, variant.Sku,
                        variant.Color, variant.Size, variant.RetailPrice, variant.WholesalePrice,
                        variant.IsActive, barcode.Barcode,
                        product.DefaultWarehouseId,
                        product.DefaultWarehouse?.Name));
            }
            catch (DbUpdateException) when (!skuProvided || !barcodeProvided)
            {
                // Unique constraint violation — regenerate identifiers and retry
                db.ChangeTracker.Clear();

                if (!skuProvided)
                    sku = await IdentifierGenerator.GenerateSkuAsync(db);
                if (!barcodeProvided)
                    barcodeValue = IdentifierGenerator.GenerateBarcode();
            }
        }

        return Problem(409, ErrorCodes.Conflict,
            "Failed to generate unique identifiers after multiple attempts. Please retry.");
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] UpdateVariantRequest req, AppDbContext db)
    {
        var variant = await db.ProductVariants
            .Include(v => v.BarcodeReservation)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (variant is null) return Problem(404, ErrorCodes.NotFound, "Variant not found.");

        // Barcode is IMMUTABLE after creation — reject any attempt to change it
        if (req.Barcode is not null)
        {
            var storedBarcode = variant.BarcodeReservation?.Barcode;
            if (!string.Equals(req.Barcode, storedBarcode, StringComparison.Ordinal))
                return Problem(409, ErrorCodes.BarcodeImmutable,
                    "Barcode cannot be changed after creation. It is immutable.");
        }

        if (req.Sku is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Sku))
                return Problem(400, ErrorCodes.ValidationFailed, "SKU cannot be empty.");

            if (await db.ProductVariants.AnyAsync(v => v.Sku == req.Sku && v.Id != id))
                return Problem(409, ErrorCodes.Conflict, $"SKU '{req.Sku}' already exists.");

            variant.Sku = req.Sku;
        }

        if (req.Color is not null) variant.Color = req.Color;
        if (req.Size is not null) variant.Size = req.Size;
        if (req.RetailPrice.HasValue) variant.RetailPrice = req.RetailPrice;
        if (req.WholesalePrice.HasValue) variant.WholesalePrice = req.WholesalePrice;
        if (req.IsActive.HasValue) variant.IsActive = req.IsActive.Value;
        if (req.LowStockThreshold.HasValue) variant.LowStockThreshold = req.LowStockThreshold;

        variant.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Variant updated." });
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db)
    {
        var variant = await db.ProductVariants
            .Include(v => v.BarcodeReservation)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (variant is null) return Problem(404, ErrorCodes.NotFound, "Variant not found.");

        // Retire barcode — never reusable
        if (variant.BarcodeReservation is not null)
        {
            variant.BarcodeReservation.Status = BarcodeStatus.Retired;
            variant.BarcodeReservation.VariantId = null;
        }

        db.ProductVariants.Remove(variant);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Variant deleted. Barcode retired." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // DTOs
    public sealed record VariantListDto(Guid Id, Guid ProductId, string ProductName,
        string Sku, string? Color, string? Size, decimal? RetailPrice,
        decimal? WholesalePrice, bool IsActive, string? Barcode,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName);
    public sealed record CreateVariantRequest(Guid ProductId, string? Sku = null, string? Barcode = null,
        string? Color = null, string? Size = null, decimal? RetailPrice = null, decimal? WholesalePrice = null,
        decimal? LowStockThreshold = null);
    public sealed record UpdateVariantRequest(string? Sku, string? Color, string? Size,
        decimal? RetailPrice, decimal? WholesalePrice, bool? IsActive,
        decimal? LowStockThreshold = null, string? Barcode = null);
}

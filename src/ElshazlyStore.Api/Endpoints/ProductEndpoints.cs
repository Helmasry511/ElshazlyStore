using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Product CRUD with paging, sorting, and search.
/// </summary>
public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        var products = group.MapGroup("/products").WithTags("Products");

        products.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsRead}");
        products.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsRead}");
        products.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsWrite}");
        products.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsWrite}");
        products.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductsWrite}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        AppDbContext db,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = "name",
        [FromQuery] bool includeTotal = true)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Product> query = db.Products
            .Include(p => p.Variants)
            .Include(p => p.DefaultWarehouse);

        query = query.ApplySearch(db.Database, q,
            p => p.Name,
            p => p.Category,
            p => p.Description);

        query = sort?.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            "category" => query.OrderBy(p => p.Category),
            "created" => query.OrderBy(p => p.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(p => p.CreatedAtUtc),
            _ => query.OrderBy(p => p.Name),
        };

        var totalCount = includeTotal ? await query.CountAsync() : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description, p.Category, p.IsActive, p.CreatedAtUtc,
                p.Variants.Count,
                p.DefaultWarehouseId,
                p.DefaultWarehouse != null ? p.DefaultWarehouse.Name : null))
            .ToListAsync();

        return Results.Ok(new PagedResult<ProductDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db)
    {
        var product = await db.Products
            .Include(p => p.Variants)
                .ThenInclude(v => v.BarcodeReservation)
            .Include(p => p.DefaultWarehouse)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return Problem(404, ErrorCodes.NotFound, "Product not found.");

        var dto = new ProductDetailDto(
            product.Id, product.Name, product.Description, product.Category,
            product.IsActive, product.CreatedAtUtc,
            product.DefaultWarehouseId,
            product.DefaultWarehouse?.Name,
            product.Variants.Select(v => new VariantDto(
                v.Id, v.ProductId, v.Sku, v.Color, v.Size, v.RetailPrice, v.WholesalePrice,
                v.IsActive, v.BarcodeReservation?.Barcode,
                product.DefaultWarehouseId,
                product.DefaultWarehouse?.Name)).ToList());

        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateProductRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(400, ErrorCodes.ValidationFailed, "Product name is required.");

        // Validate DefaultWarehouseId if provided
        Warehouse? defaultWarehouse = null;
        if (req.DefaultWarehouseId.HasValue)
        {
            defaultWarehouse = await db.Warehouses.FindAsync(req.DefaultWarehouseId.Value);
            if (defaultWarehouse is null)
                return Problem(404, ErrorCodes.WarehouseNotFound, "Default warehouse not found.");
            if (!defaultWarehouse.IsActive)
                return Problem(422, ErrorCodes.WarehouseInactive, "Default warehouse is inactive.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            Category = req.Category,
            DefaultWarehouseId = req.DefaultWarehouseId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/products/{product.Id}",
            new ProductDto(product.Id, product.Name, product.Description, product.Category,
                product.IsActive, product.CreatedAtUtc, 0,
                product.DefaultWarehouseId, defaultWarehouse?.Name));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] UpdateProductRequest req, AppDbContext db)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return Problem(404, ErrorCodes.NotFound, "Product not found.");

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Problem(400, ErrorCodes.ValidationFailed, "Product name cannot be empty.");
            product.Name = req.Name;
        }

        if (req.Description is not null) product.Description = req.Description;
        if (req.Category is not null) product.Category = req.Category;
        if (req.IsActive.HasValue) product.IsActive = req.IsActive.Value;

        // Handle DefaultWarehouseId: null = don't change, Guid.Empty = clear, valid GUID = set
        if (req.DefaultWarehouseId is not null)
        {
            if (req.DefaultWarehouseId.Value == Guid.Empty)
            {
                // Explicit clear
                product.DefaultWarehouseId = null;
            }
            else
            {
                var warehouse = await db.Warehouses.FindAsync(req.DefaultWarehouseId.Value);
                if (warehouse is null)
                    return Problem(404, ErrorCodes.WarehouseNotFound, "Default warehouse not found.");
                if (!warehouse.IsActive)
                    return Problem(422, ErrorCodes.WarehouseInactive, "Default warehouse is inactive.");
                product.DefaultWarehouseId = warehouse.Id;
            }
        }

        product.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Product updated." });
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db)
    {
        var product = await db.Products
            .Include(p => p.Variants)
                .ThenInclude(v => v.BarcodeReservation)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return Problem(404, ErrorCodes.NotFound, "Product not found.");

        // Retire all barcodes for this product's variants
        foreach (var variant in product.Variants)
        {
            if (variant.BarcodeReservation is not null)
            {
                variant.BarcodeReservation.Status = BarcodeStatus.Retired;
                variant.BarcodeReservation.VariantId = null;
            }
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Product deleted." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // DTOs
    public sealed record ProductDto(Guid Id, string Name, string? Description, string? Category,
        bool IsActive, DateTime CreatedAtUtc, int VariantCount,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName);
    public sealed record ProductDetailDto(Guid Id, string Name, string? Description, string? Category,
        bool IsActive, DateTime CreatedAtUtc,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName,
        List<VariantDto> Variants);
    public sealed record VariantDto(Guid Id, Guid ProductId, string Sku, string? Color, string? Size,
        decimal? RetailPrice, decimal? WholesalePrice, bool IsActive, string? Barcode,
        Guid? DefaultWarehouseId, string? DefaultWarehouseName);
    public sealed record CreateProductRequest(string Name, string? Description, string? Category,
        Guid? DefaultWarehouseId = null);
    public sealed record UpdateProductRequest(string? Name, string? Description, string? Category, bool? IsActive,
        Guid? DefaultWarehouseId = null);
}

/// <summary>Generic paged result wrapper.</summary>
public sealed record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

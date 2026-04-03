using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Warehouse CRUD endpoints.
/// </summary>
public static class WarehouseEndpoints
{
    public static RouteGroupBuilder MapWarehouseEndpoints(this RouteGroupBuilder group)
    {
        var warehouses = group.MapGroup("/warehouses").WithTags("Warehouses");

        warehouses.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.WarehousesRead}");
        warehouses.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.WarehousesRead}");
        warehouses.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.WarehousesWrite}");
        warehouses.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.WarehousesWrite}");
        warehouses.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.WarehousesWrite}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        AppDbContext db,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = db.Warehouses.AsNoTracking().AsQueryable();

        query = query.ApplySearch(db.Database, q,
            w => w.Code,
            w => w.Name);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;

        var items = await query
            .OrderBy(w => w.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Address, w.IsDefault, w.IsActive, w.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var w = await db.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return w is null
            ? Results.Problem(statusCode: 404, title: "Not Found", detail: "Warehouse not found.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.NotFound })
            : Results.Ok(new WarehouseDto(w.Id, w.Code, w.Name, w.Address, w.IsDefault, w.IsActive, w.CreatedAtUtc));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateWarehouseRequest req, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
            return Results.Problem(statusCode: 400, title: "Validation Failed",
                detail: "Code and Name are required.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.ValidationFailed });

        if (await db.Warehouses.AnyAsync(w => w.Code == req.Code.Trim(), ct))
            return Results.Problem(statusCode: 409, title: "Conflict",
                detail: $"Warehouse code '{req.Code}' already exists.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.Conflict });

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = req.Code.Trim(),
            Name = req.Name.Trim(),
            Address = req.Address?.Trim(),
            IsDefault = req.IsDefault,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/warehouses/{warehouse.Id}",
            new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address,
                warehouse.IsDefault, warehouse.IsActive, warehouse.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] UpdateWarehouseRequest req, AppDbContext db, CancellationToken ct)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (warehouse is null)
            return Results.Problem(statusCode: 404, title: "Not Found", detail: "Warehouse not found.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.NotFound });

        if (!string.IsNullOrWhiteSpace(req.Name)) warehouse.Name = req.Name.Trim();
        if (req.Address is not null) warehouse.Address = req.Address.Trim();
        if (req.IsActive.HasValue) warehouse.IsActive = req.IsActive.Value;
        warehouse.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address,
            warehouse.IsDefault, warehouse.IsActive, warehouse.CreatedAtUtc));
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (warehouse is null)
            return Results.Problem(statusCode: 404, title: "Not Found", detail: "Warehouse not found.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.NotFound });

        // Soft-delete
        warehouse.IsActive = false;
        warehouse.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ── DTOs ──

    private sealed record WarehouseDto(Guid Id, string Code, string Name, string? Address,
        bool IsDefault, bool IsActive, DateTime CreatedAtUtc);

    private sealed record CreateWarehouseRequest(string Code, string Name, string? Address, bool IsDefault = false);
    private sealed record UpdateWarehouseRequest(string? Name, string? Address, bool? IsActive);
}

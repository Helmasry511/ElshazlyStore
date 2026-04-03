using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Supplier CRUD with paging, sorting, and search across code/name/phone.
/// </summary>
public static class SupplierEndpoints
{
    public static RouteGroupBuilder MapSupplierEndpoints(this RouteGroupBuilder group)
    {
        var suppliers = group.MapGroup("/suppliers").WithTags("Suppliers");

        suppliers.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.SuppliersRead}");
        suppliers.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.SuppliersRead}");
        suppliers.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.SuppliersWrite}");
        suppliers.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.SuppliersWrite}");
        suppliers.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.SuppliersWrite}");

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

        IQueryable<Supplier> query = db.Suppliers;

        query = query.ApplySearch(db.Database, q,
            s => s.Code,
            s => s.Name,
            s => s.Phone,
            s => s.Phone2);

        query = sort?.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(s => s.Name),
            "name_desc" => query.OrderByDescending(s => s.Name),
            "code" => query.OrderBy(s => s.Code),
            "created" => query.OrderBy(s => s.CreatedAtUtc),
            "created_desc" => query.OrderByDescending(s => s.CreatedAtUtc),
            _ => query.OrderBy(s => s.Name),
        };

        var totalCount = includeTotal ? await query.CountAsync() : -1;
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierDto(s.Id, s.Code, s.Name, s.Phone, s.Phone2, s.Notes, s.IsActive, s.CreatedAtUtc))
            .ToListAsync();

        return Results.Ok(new PagedResult<SupplierDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null)
            return Problem(404, ErrorCodes.NotFound, "Supplier not found.");

        return Results.Ok(new SupplierDto(supplier.Id, supplier.Code, supplier.Name,
            supplier.Phone, supplier.Phone2, supplier.Notes, supplier.IsActive, supplier.CreatedAtUtc));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateSupplierRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(400, ErrorCodes.ValidationFailed, "Supplier name is required.");

        var code = req.Code;
        if (string.IsNullOrWhiteSpace(code))
        {
            var maxCode = await db.Suppliers
                .Where(s => s.Code.StartsWith("SUP-"))
                .Select(s => s.Code)
                .OrderByDescending(s => s)
                .FirstOrDefaultAsync();

            var nextSeq = 1;
            if (maxCode is not null && int.TryParse(maxCode.Replace("SUP-", ""), out var parsed))
                nextSeq = parsed + 1;

            code = $"SUP-{nextSeq:D6}";
        }
        else
        {
            if (await db.Suppliers.AnyAsync(s => s.Code == code))
                return Problem(409, ErrorCodes.Conflict, $"Supplier code '{code}' already exists.");
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = req.Name,
            Phone = req.Phone,
            Phone2 = req.Phone2,
            Notes = req.Notes,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/suppliers/{supplier.Id}",
            new SupplierDto(supplier.Id, supplier.Code, supplier.Name,
                supplier.Phone, supplier.Phone2, supplier.Notes, supplier.IsActive, supplier.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] UpdateSupplierRequest req, AppDbContext db)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return Problem(404, ErrorCodes.NotFound, "Supplier not found.");

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Problem(400, ErrorCodes.ValidationFailed, "Name cannot be empty.");
            supplier.Name = req.Name;
        }

        if (req.Code is not null)
        {
            if (await db.Suppliers.AnyAsync(s => s.Code == req.Code && s.Id != id))
                return Problem(409, ErrorCodes.Conflict, $"Supplier code '{req.Code}' already exists.");
            supplier.Code = req.Code;
        }

        if (req.Phone is not null) supplier.Phone = req.Phone;
        if (req.Phone2 is not null) supplier.Phone2 = req.Phone2;
        if (req.Notes is not null) supplier.Notes = req.Notes;
        if (req.IsActive.HasValue) supplier.IsActive = req.IsActive.Value;

        supplier.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Supplier updated." });
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return Problem(404, ErrorCodes.NotFound, "Supplier not found.");

        supplier.IsActive = false;
        supplier.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Supplier deactivated." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // DTOs
    public sealed record SupplierDto(Guid Id, string Code, string Name, string? Phone,
        string? Phone2, string? Notes, bool IsActive, DateTime CreatedAtUtc);
    public sealed record CreateSupplierRequest(string Name, string? Code, string? Phone,
        string? Phone2, string? Notes);
    public sealed record UpdateSupplierRequest(string? Name, string? Code, string? Phone,
        string? Phone2, string? Notes, bool? IsActive);
}

using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Admin-only role management: CRUD + permission assignment.
/// </summary>
public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(this RouteGroupBuilder group)
    {
        var roles = group.MapGroup("/roles").WithTags("Roles");

        roles.MapGet("/", ListRolesAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesRead}");
        roles.MapGet("/{id:guid}", GetRoleAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesRead}");
        roles.MapPost("/", CreateRoleAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesWrite}");
        roles.MapPut("/{id:guid}", UpdateRoleAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesWrite}");
        roles.MapDelete("/{id:guid}", DeleteRoleAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesWrite}");

        // Permission management
        roles.MapGet("/{id:guid}/permissions", GetRolePermissionsAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesRead}");
        roles.MapPut("/{id:guid}/permissions", SetRolePermissionsAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesWrite}");

        // List all available permissions
        roles.MapGet("/permissions/all", ListAllPermissionsAsync)
            .RequireAuthorization($"Permission:{Permissions.RolesRead}");

        return group;
    }

    private static async Task<IResult> ListRolesAsync(AppDbContext db)
    {
        var roles = await db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.CreatedAtUtc,
                r.RolePermissions.Select(rp => rp.Permission.Code).ToList()))
            .ToListAsync();

        return Results.Ok(roles);
    }

    private static async Task<IResult> GetRoleAsync(Guid id, AppDbContext db)
    {
        var role = await db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role is null) return Problem(404, ErrorCodes.NotFound, "Role not found.");

        return Results.Ok(new RoleDto(role.Id, role.Name, role.Description, role.CreatedAtUtc,
            role.RolePermissions.Select(rp => rp.Permission.Code).ToList()));
    }

    private static async Task<IResult> CreateRoleAsync(
        [FromBody] CreateRoleRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(400, ErrorCodes.ValidationFailed, "Role name is required.");

        if (await db.Roles.AnyAsync(r => r.Name == req.Name))
            return Problem(409, ErrorCodes.Conflict, $"Role '{req.Name}' already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/roles/{role.Id}",
            new RoleDto(role.Id, role.Name, role.Description, role.CreatedAtUtc, []));
    }

    private static async Task<IResult> UpdateRoleAsync(
        Guid id, [FromBody] UpdateRoleRequest req, AppDbContext db)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is null) return Problem(404, ErrorCodes.NotFound, "Role not found.");

        if (req.Name is not null)
        {
            if (await db.Roles.AnyAsync(r => r.Name == req.Name && r.Id != id))
                return Problem(409, ErrorCodes.Conflict, $"Role '{req.Name}' already exists.");

            role.Name = req.Name;
        }

        if (req.Description is not null)
            role.Description = req.Description;

        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Role updated." });
    }

    private static async Task<IResult> DeleteRoleAsync(Guid id, AppDbContext db)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is null) return Problem(404, ErrorCodes.NotFound, "Role not found.");

        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Role deleted." });
    }

    private static async Task<IResult> GetRolePermissionsAsync(Guid id, AppDbContext db)
    {
        var role = await db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role is null) return Problem(404, ErrorCodes.NotFound, "Role not found.");

        var perms = role.RolePermissions
            .Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.Code, rp.Permission.Description))
            .ToList();

        return Results.Ok(perms);
    }

    private static async Task<IResult> SetRolePermissionsAsync(
        Guid id, [FromBody] SetPermissionsRequest req, AppDbContext db)
    {
        var role = await db.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return Problem(404, ErrorCodes.NotFound, "Role not found.");

        var validPermIds = await db.Permissions
            .Where(p => req.PermissionCodes.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();

        role.RolePermissions.Clear();
        foreach (var pid in validPermIds)
            role.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = pid });

        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Permissions updated." });
    }

    private static async Task<IResult> ListAllPermissionsAsync(AppDbContext db)
    {
        var perms = await db.Permissions
            .OrderBy(p => p.Code)
            .Select(p => new PermissionDto(p.Id, p.Code, p.Description))
            .ToListAsync();

        return Results.Ok(perms);
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // DTOs
    public sealed record RoleDto(Guid Id, string Name, string? Description, DateTime CreatedAtUtc, List<string> Permissions);
    public sealed record CreateRoleRequest(string Name, string? Description);
    public sealed record UpdateRoleRequest(string? Name, string? Description);
    public sealed record SetPermissionsRequest(List<string> PermissionCodes);
    public sealed record PermissionDto(Guid Id, string Code, string? Description);
}

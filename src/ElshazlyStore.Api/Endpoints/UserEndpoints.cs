using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Admin-only user management: list, get, create, update, deactivate.
/// </summary>
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        var users = group.MapGroup("/users").WithTags("Users");

        users.MapGet("/", ListUsersAsync)
            .RequireAuthorization($"Permission:{Permissions.UsersRead}");
        users.MapGet("/{id:guid}", GetUserAsync)
            .RequireAuthorization($"Permission:{Permissions.UsersRead}");
        users.MapPost("/", CreateUserAsync)
            .RequireAuthorization($"Permission:{Permissions.UsersWrite}");
        users.MapPut("/{id:guid}", UpdateUserAsync)
            .RequireAuthorization($"Permission:{Permissions.UsersWrite}");
        users.MapDelete("/{id:guid}", DeactivateUserAsync)
            .RequireAuthorization($"Permission:{Permissions.UsersWrite}");

        return group;
    }

    private static async Task<IResult> ListUsersAsync(AppDbContext db)
    {
        var users = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.Username, u.IsActive, u.CreatedAtUtc,
                u.UserRoles.Select(ur => ur.Role.Name).ToList()))
            .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> GetUserAsync(Guid id, AppDbContext db)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return Problem(404, ErrorCodes.NotFound, "User not found.");

        return Results.Ok(new UserDto(user.Id, user.Username, user.IsActive, user.CreatedAtUtc,
            user.UserRoles.Select(ur => ur.Role.Name).ToList()));
    }

    private static async Task<IResult> CreateUserAsync(
        [FromBody] CreateUserRequest req, AppDbContext db, IPasswordHasher hasher)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return Problem(400, ErrorCodes.ValidationFailed, "Username and password are required.");

        if (req.Username.Length < 3)
            return Problem(400, ErrorCodes.ValidationFailed, "Username must be at least 3 characters.");

        if (req.Password.Length < 6)
            return Problem(400, ErrorCodes.ValidationFailed, "Password must be at least 6 characters.");

        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Problem(409, ErrorCodes.Conflict, $"Username '{req.Username}' already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            PasswordHash = hasher.Hash(req.Password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        // Assign roles if provided
        if (req.RoleIds is { Count: > 0 })
        {
            var validRoles = await db.Roles
                .Where(r => req.RoleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync();

            foreach (var roleId in validRoles)
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/users/{user.Id}",
            new UserDto(user.Id, user.Username, user.IsActive, user.CreatedAtUtc, []));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid id, [FromBody] UpdateUserRequest req, AppDbContext db, IPasswordHasher hasher)
    {
        var user = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return Problem(404, ErrorCodes.NotFound, "User not found.");

        if (req.Username is not null)
        {
            if (req.Username.Length < 3)
                return Problem(400, ErrorCodes.ValidationFailed, "Username must be at least 3 characters.");

            if (await db.Users.AnyAsync(u => u.Username == req.Username && u.Id != id))
                return Problem(409, ErrorCodes.Conflict, $"Username '{req.Username}' already exists.");

            user.Username = req.Username;
        }

        if (req.Password is not null)
        {
            if (req.Password.Length < 6)
                return Problem(400, ErrorCodes.ValidationFailed, "Password must be at least 6 characters.");

            user.PasswordHash = hasher.Hash(req.Password);
        }

        if (req.IsActive.HasValue)
            user.IsActive = req.IsActive.Value;

        // Replace roles if provided
        if (req.RoleIds is not null)
        {
            user.UserRoles.Clear();
            var validRoles = await db.Roles
                .Where(r => req.RoleIds.Contains(r.Id))
                .Select(r => r.Id).ToListAsync();

            foreach (var roleId in validRoles)
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }

        await db.SaveChangesAsync();
        return Results.Ok(new { message = "User updated." });
    }

    private static async Task<IResult> DeactivateUserAsync(Guid id, AppDbContext db)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return Problem(404, ErrorCodes.NotFound, "User not found.");

        user.IsActive = false;
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "User deactivated." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // DTOs
    public sealed record UserDto(Guid Id, string Username, bool IsActive, DateTime CreatedAtUtc, List<string> Roles);
    public sealed record CreateUserRequest(string Username, string Password, List<Guid>? RoleIds);
    public sealed record UpdateUserRequest(string? Username, string? Password, bool? IsActive, List<Guid>? RoleIds);
}

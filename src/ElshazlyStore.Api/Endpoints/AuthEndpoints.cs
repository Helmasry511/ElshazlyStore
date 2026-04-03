using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Authentication endpoints: login, refresh, logout, me.
/// </summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync).AllowAnonymous();
        auth.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapGet("/me", MeAsync).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        AppDbContext db,
        IPasswordHasher hasher,
        ITokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return ProblemResult(400, ErrorCodes.ValidationFailed, "Username and password are required.");

        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            return ProblemResult(401, ErrorCodes.InvalidCredentials, "Invalid username or password.");

        if (!user.IsActive)
            return ProblemResult(403, ErrorCodes.AccountInactive, "Account is deactivated.");

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var accessToken = tokenService.GenerateAccessToken(user, permissions);
        var (rawRefreshToken, refreshTokenEntity) = tokenService.GenerateRefreshToken(user.Id);

        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync();

        return Results.Ok(new LoginResponse(accessToken, rawRefreshToken, refreshTokenEntity.ExpiresAtUtc));
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        AppDbContext db,
        ITokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ProblemResult(400, ErrorCodes.ValidationFailed, "Refresh token is required.");

        var incomingHash = tokenService.HashToken(request.RefreshToken);

        var token = await db.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash);

        if (token is null)
            return ProblemResult(401, ErrorCodes.TokenInvalid, "Refresh token not found.");

        if (!token.IsActive)
            return ProblemResult(401, ErrorCodes.TokenExpired, "Refresh token is expired or revoked.");

        if (!token.User.IsActive)
            return ProblemResult(403, ErrorCodes.AccountInactive, "Account is deactivated.");

        // Rotate: revoke old, issue new
        token.RevokedAtUtc = DateTime.UtcNow;
        var (rawNewRefresh, newRefreshEntity) = tokenService.GenerateRefreshToken(token.UserId);
        token.ReplacedByTokenHash = newRefreshEntity.TokenHash;
        db.RefreshTokens.Add(newRefreshEntity);

        var permissions = token.User.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var accessToken = tokenService.GenerateAccessToken(token.User, permissions);
        await db.SaveChangesAsync();

        return Results.Ok(new LoginResponse(accessToken, rawNewRefresh, newRefreshEntity.ExpiresAtUtc));
    }

    private static async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        AppDbContext db,
        ICurrentUserService currentUser,
        ITokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ProblemResult(400, ErrorCodes.ValidationFailed, "Refresh token is required.");

        var hash = tokenService.HashToken(request.RefreshToken);

        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && rt.UserId == currentUser.UserId);

        if (token is not null && token.IsActive)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { message = "Logged out." });
    }

    private static async Task<IResult> MeAsync(
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        if (currentUser.UserId is null)
            return ProblemResult(401, ErrorCodes.Unauthorized, "Not authenticated.");

        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId);

        if (user is null)
            return ProblemResult(404, ErrorCodes.NotFound, "User not found.");

        return Results.Ok(new MeResponse(
            user.Id, user.Username, user.IsActive,
            user.UserRoles.Select(ur => ur.Role.Name).ToList()));
    }

    private static IResult ProblemResult(int status, string errorCode, string detail)
        => Results.Problem(detail: detail, title: errorCode, statusCode: status,
            type: $"https://elshazlystore.local/errors/{errorCode.ToLowerInvariant()}");

    // DTOs
    public sealed record LoginRequest(string Username, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
    public sealed record MeResponse(Guid Id, string Username, bool IsActive, List<string> Roles);
}

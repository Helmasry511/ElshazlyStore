using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Endpoints for the global reason-code catalog.
/// Admin-only for mutations; staff can view.
/// </summary>
public static class ReasonCodeEndpoints
{
    public static RouteGroupBuilder MapReasonCodeEndpoints(this RouteGroupBuilder group)
    {
        var reasons = group.MapGroup("/reasons").WithTags("Reason Codes");

        // Read — VIEW_REASON_CODES (staff)
        reasons.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewReasonCodes}");
        reasons.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewReasonCodes}");

        // Write — MANAGE_REASON_CODES (admin)
        reasons.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.ManageReasonCodes}");
        reasons.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.ManageReasonCodes}");
        reasons.MapPost("/{id:guid}/disable", DisableAsync)
            .RequireAuthorization($"Permission:{Permissions.ManageReasonCodes}");

        return group;
    }

    // ── Handlers ──

    private static async Task<IResult> ListAsync(
        ReasonCodeService svc,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        ReasonCategory? cat = null;
        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<ReasonCategory>(category, ignoreCase: true, out var parsed))
        {
            cat = parsed;
        }

        var (items, totalCount) = await svc.ListAsync(cat, isActive, q, page, pageSize, includeTotal, ct);
        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    private static async Task<IResult> GetAsync(
        Guid id, ReasonCodeService svc, CancellationToken ct)
    {
        var dto = await svc.GetAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.ReasonCodeNotFound, "Reason code not found.");
        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateReasonCodeRequest req,
        ReasonCodeService svc,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        if (string.IsNullOrWhiteSpace(req.Code))
            return Problem(400, ErrorCodes.ValidationFailed, "Code is required.");
        if (string.IsNullOrWhiteSpace(req.NameAr))
            return Problem(400, ErrorCodes.ValidationFailed, "NameAr is required.");

        if (!Enum.TryParse<ReasonCategory>(req.Category, ignoreCase: true, out var category))
            return Problem(400, ErrorCodes.ValidationFailed,
                $"Invalid category. Valid values: {string.Join(", ", Enum.GetNames<ReasonCategory>())}");

        var (result, errorCode, errorDetail) = await svc.CreateAsync(
            req.Code.Trim().ToUpperInvariant(), req.NameAr.Trim(), req.Description?.Trim(),
            category, req.RequiresManagerApproval,
            currentUser.UserId.Value, ct);

        if (result is null)
        {
            var status = errorCode switch
            {
                ErrorCodes.ReasonCodeAlreadyExists => 409,
                _ => 400,
            };
            return Problem(status, errorCode!, errorDetail!);
        }

        return Results.Created($"/api/v1/reasons/{result.Id}", result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateReasonCodeRequest req,
        ReasonCodeService svc,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        ReasonCategory? cat = null;
        if (req.Category is not null)
        {
            if (!Enum.TryParse<ReasonCategory>(req.Category, ignoreCase: true, out var parsed))
                return Problem(400, ErrorCodes.ValidationFailed,
                    $"Invalid category. Valid values: {string.Join(", ", Enum.GetNames<ReasonCategory>())}");
            cat = parsed;
        }

        var (success, errorCode, errorDetail) = await svc.UpdateAsync(
            id, req.NameAr?.Trim(), req.Description?.Trim(),
            cat, req.RequiresManagerApproval,
            currentUser.UserId.Value, ct);

        if (!success)
        {
            var status = errorCode switch
            {
                ErrorCodes.ReasonCodeNotFound => 404,
                _ => 400,
            };
            return Problem(status, errorCode!, errorDetail!);
        }

        return Results.Ok(new { message = "Reason code updated." });
    }

    private static async Task<IResult> DisableAsync(
        Guid id,
        ReasonCodeService svc,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        var (success, errorCode, errorDetail) = await svc.DisableAsync(
            id, currentUser.UserId.Value, ct);

        if (!success)
            return Problem(404, errorCode!, errorDetail!);

        return Results.Ok(new { message = "Reason code disabled." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── Request DTOs ──

    public sealed record CreateReasonCodeRequest(
        string Code,
        string NameAr,
        string? Description,
        string Category,
        bool RequiresManagerApproval = false);

    public sealed record UpdateReasonCodeRequest(
        string? NameAr,
        string? Description,
        string? Category,
        bool? RequiresManagerApproval);
}

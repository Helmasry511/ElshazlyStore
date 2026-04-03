using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

public static class DispositionEndpoints
{
    public static RouteGroupBuilder MapDispositionEndpoints(this RouteGroupBuilder group)
    {
        var dispositions = group.MapGroup("/dispositions").WithTags("Inventory Dispositions");

        dispositions.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewDispositions}");
        dispositions.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewDispositions}");
        dispositions.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.DispositionCreate}");
        dispositions.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.DispositionCreate}");
        dispositions.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.DispositionCreate}");
        dispositions.MapPost("/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization($"Permission:{Permissions.DispositionApprove}");
        dispositions.MapPost("/{id:guid}/post", PostAsync)
            .RequireAuthorization($"Permission:{Permissions.DispositionPost}");
        dispositions.MapPost("/{id:guid}/void", VoidAsync)
            .RequireAuthorization($"Permission:{Permissions.DispositionVoid}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        DispositionService service,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = null,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await service.ListAsync(q, page, pageSize, sort, includeTotal, ct);
        return Results.Ok(new PagedResult<DispositionService.DispositionDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, DispositionService service, CancellationToken ct)
    {
        var dto = await service.GetByIdAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.DispositionNotFound, "Disposition not found.");
        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateDispositionRequest req,
        DispositionService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        if (req.WarehouseId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Warehouse ID is required.");
        if (req.Lines is null || req.Lines.Count == 0)
            return Problem(400, ErrorCodes.DispositionEmpty, "At least one line is required.");

        var lines = req.Lines.Select(l =>
            new DispositionService.DispositionLineRequest(
                l.VariantId, l.Quantity, l.ReasonCodeId, l.DispositionType, l.Notes)).ToList();
        var request = new DispositionService.CreateDispositionRequest(
            req.WarehouseId, req.DispositionDateUtc, req.Notes, lines);
        var (result, errorCode, errorDetail) = await service.CreateAsync(
            request, currentUser.UserId.Value, ct);

        if (result is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound
                    or ErrorCodes.ReasonCodeNotFound => 404,
                ErrorCodes.DispositionNumberExists => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }
        return Results.Created($"/api/v1/dispositions/{result.Id}", result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateDispositionRequest req,
        DispositionService service,
        CancellationToken ct)
    {
        var lines = req.Lines?.Select(l =>
            new DispositionService.DispositionLineRequest(
                l.VariantId, l.Quantity, l.ReasonCodeId, l.DispositionType, l.Notes)).ToList();
        var request = new DispositionService.UpdateDispositionRequest(
            req.WarehouseId, req.Notes, lines);
        var (result, errorCode, errorDetail) = await service.UpdateAsync(id, request, ct);

        if (result is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.DispositionNotFound => 404,
                ErrorCodes.DispositionAlreadyPosted => 409,
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound
                    or ErrorCodes.ReasonCodeNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteAsync(Guid id, DispositionService service, CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await service.DeleteAsync(id, ct);
        if (!success)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.DispositionNotFound => 404,
                ErrorCodes.DispositionAlreadyPosted => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }
        return Results.Ok(new { message = "Disposition deleted." });
    }

    private static async Task<IResult> ApproveAsync(
        Guid id, DispositionService service, ICurrentUserService currentUser, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        var result = await service.ApproveAsync(id, currentUser.UserId.Value, ct);
        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.DispositionNotFound => 404,
                ErrorCodes.DispositionAlreadyPosted => 409,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }
        return Results.Ok(new { message = "Disposition approved." });
    }

    private static async Task<IResult> PostAsync(
        Guid id, DispositionService service, ICurrentUserService currentUser, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        var result = await service.PostAsync(id, currentUser.UserId.Value, ct);
        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.DispositionNotFound => 404,
                ErrorCodes.DispositionAlreadyPosted or ErrorCodes.PostAlreadyPosted => 409,
                ErrorCodes.PostConcurrencyConflict => 409,
                ErrorCodes.DispositionAlreadyVoided => 409,
                ErrorCodes.StockNegativeNotAllowed => 422,
                ErrorCodes.ReasonCodeInactive => 400,
                ErrorCodes.DispositionRequiresApproval => 403,
                ErrorCodes.DestinationWarehouseNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }
        return Results.Ok(new { stockMovementId = result.StockMovementId });
    }

    private static async Task<IResult> VoidAsync(
        Guid id, DispositionService service, ICurrentUserService currentUser, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        var result = await service.VoidAsync(id, currentUser.UserId.Value, ct);
        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.DispositionNotFound => 404,
                ErrorCodes.DispositionAlreadyVoided => 409,
                ErrorCodes.DispositionVoidNotAllowedAfterPost => 409,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }
        return Results.Ok(new { message = "Disposition voided." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──
    public sealed record CreateDispositionRequest(
        Guid WarehouseId, DateTime? DispositionDateUtc,
        string? Notes, List<DispositionLineRequest> Lines);
    public sealed record UpdateDispositionRequest(
        Guid? WarehouseId, string? Notes,
        List<DispositionLineRequest>? Lines);
    public sealed record DispositionLineRequest(
        Guid VariantId, decimal Quantity,
        Guid ReasonCodeId, DispositionType DispositionType, string? Notes);
}

using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Stock movement posting endpoint.
/// Single entry point for all stock mutations — no direct balance manipulation.
/// </summary>
public static class StockMovementEndpoints
{
    public static RouteGroupBuilder MapStockMovementEndpoints(this RouteGroupBuilder group)
    {
        var movements = group.MapGroup("/stock-movements").WithTags("StockMovements");

        movements.MapPost("/post", PostAsync)
            .RequireAuthorization($"Permission:{Permissions.StockPost}");

        return group;
    }

    private static async Task<IResult> PostAsync(
        [FromBody] PostRequest req,
        StockService stockService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Results.Problem(statusCode: 401, title: "Unauthorized",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.Unauthorized });

        if (req.Lines is not { Count: > 0 })
            return Results.Problem(statusCode: 400, title: "Validation Failed",
                detail: "Movement must have at least one line.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.MovementEmpty });

        var lines = req.Lines.Select(l => new StockService.PostMovementLineRequest(
            l.VariantId, l.WarehouseId, l.QuantityDelta, l.UnitCost, l.Reason)).ToList();

        var request = new StockService.PostMovementRequest(req.Type, req.Reference, req.Notes, lines);
        var result = await stockService.PostAsync(request, currentUser.UserId.Value, ct);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.StockNegativeNotAllowed => 422,
                ErrorCodes.VariantNotFound or ErrorCodes.WarehouseNotFound => 404,
                ErrorCodes.Conflict => 409,
                ErrorCodes.TransferUnbalanced => 400,
                _ => 400,
            };

            return Results.Problem(statusCode: statusCode, title: "Stock Movement Failed",
                detail: result.ErrorDetail,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.ErrorCode });
        }

        return Results.Ok(new { movementId = result.MovementId });
    }

    // ── DTOs ──

    private sealed record PostRequest(
        MovementType Type,
        string? Reference,
        string? Notes,
        List<PostLineRequest> Lines);

    private sealed record PostLineRequest(
        Guid VariantId,
        Guid WarehouseId,
        decimal QuantityDelta,
        decimal? UnitCost,
        string? Reason);
}

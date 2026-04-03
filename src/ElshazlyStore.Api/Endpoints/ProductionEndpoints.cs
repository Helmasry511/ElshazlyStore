using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Production batch CRUD, posting, and search endpoints.
/// Posting creates TWO stock movements atomically:
///   ProductionConsume + ProductionProduce.
/// </summary>
public static class ProductionEndpoints
{
    public static RouteGroupBuilder MapProductionEndpoints(this RouteGroupBuilder group)
    {
        var production = group.MapGroup("/production").WithTags("Production");

        production.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductionRead}");
        production.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductionRead}");
        production.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductionWrite}");
        production.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductionWrite}");
        production.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductionWrite}");
        production.MapPost("/{id:guid}/post", PostAsync)
            .RequireAuthorization($"Permission:{Permissions.ProductionPost}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        ProductionService productionService,
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

        var (items, totalCount) = await productionService.ListAsync(q, page, pageSize, sort, includeTotal, ct);
        return Results.Ok(new PagedResult<ProductionService.BatchDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ProductionService productionService,
        CancellationToken ct)
    {
        var dto = await productionService.GetByIdAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.ProductionBatchNotFound, "Production batch not found.");

        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateProductionRequest req,
        ProductionService productionService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        if (req.WarehouseId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Warehouse ID is required.");

        var inputs = req.Inputs.Select(l =>
            new ProductionService.BatchLineRequest(l.VariantId, l.Quantity, l.UnitCost)).ToList();
        var outputs = req.Outputs.Select(l =>
            new ProductionService.BatchLineRequest(l.VariantId, l.Quantity, l.UnitCost)).ToList();

        var request = new ProductionService.CreateBatchRequest(
            req.WarehouseId, req.BatchNumber, req.Notes, inputs, outputs);

        var (batch, errorCode, errorDetail) = await productionService.CreateAsync(
            request, currentUser.UserId.Value, ct);

        if (batch is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound => 404,
                ErrorCodes.BatchNumberExists => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Created($"/api/v1/production/{batch.Id}", batch);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateProductionRequest req,
        ProductionService productionService,
        CancellationToken ct)
    {
        var inputs = req.Inputs?.Select(l =>
            new ProductionService.BatchLineRequest(l.VariantId, l.Quantity, l.UnitCost)).ToList();
        var outputs = req.Outputs?.Select(l =>
            new ProductionService.BatchLineRequest(l.VariantId, l.Quantity, l.UnitCost)).ToList();

        var request = new ProductionService.UpdateBatchRequest(
            req.WarehouseId, req.Notes, inputs, outputs);

        var (batch, errorCode, errorDetail) = await productionService.UpdateAsync(id, request, ct);

        if (batch is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.ProductionBatchNotFound => 404,
                ErrorCodes.ProductionBatchAlreadyPosted => 409,
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(batch);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ProductionService productionService,
        CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await productionService.DeleteAsync(id, ct);

        if (!success)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.ProductionBatchNotFound => 404,
                ErrorCodes.ProductionBatchAlreadyPosted => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(new { message = "Production batch deleted." });
    }

    private static async Task<IResult> PostAsync(
        Guid id,
        ProductionService productionService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        var result = await productionService.PostAsync(id, currentUser.UserId.Value, ct);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.ProductionBatchNotFound => 404,
                ErrorCodes.ProductionBatchAlreadyPosted => 409,
                ErrorCodes.PostConcurrencyConflict => 409,
                ErrorCodes.StockNegativeNotAllowed => 422,
                ErrorCodes.VariantNotFound or ErrorCodes.WarehouseNotFound => 404,
                ErrorCodes.Conflict => 409,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }

        return Results.Ok(new
        {
            consumeMovementId = result.ConsumeMovementId,
            produceMovementId = result.ProduceMovementId,
        });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──

    public sealed record CreateProductionRequest(
        Guid WarehouseId,
        string? BatchNumber,
        string? Notes,
        List<ProductionLineRequest> Inputs,
        List<ProductionLineRequest> Outputs);

    public sealed record UpdateProductionRequest(
        Guid? WarehouseId,
        string? Notes,
        List<ProductionLineRequest>? Inputs,
        List<ProductionLineRequest>? Outputs);

    public sealed record ProductionLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal? UnitCost);
}

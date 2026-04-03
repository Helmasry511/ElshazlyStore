using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Purchase receipt CRUD, posting, and search endpoints.
/// Posting a receipt creates a StockMovement of type PurchaseReceipt.
/// </summary>
public static class PurchaseEndpoints
{
    public static RouteGroupBuilder MapPurchaseEndpoints(this RouteGroupBuilder group)
    {
        var purchases = group.MapGroup("/purchases").WithTags("Purchases");

        purchases.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchasesRead}");
        purchases.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchasesRead}");
        purchases.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchasesWrite}");
        purchases.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchasesWrite}");
        purchases.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchasesWrite}");
        purchases.MapPost("/{id:guid}/post", PostAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchasesPost}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        PurchaseService purchaseService,
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

        var (items, totalCount) = await purchaseService.ListAsync(q, page, pageSize, sort, includeTotal, ct);
        return Results.Ok(new PagedResult<PurchaseService.ReceiptDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        PurchaseService purchaseService,
        CancellationToken ct)
    {
        var dto = await purchaseService.GetByIdAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.PurchaseReceiptNotFound, "Purchase receipt not found.");

        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreatePurchaseRequest req,
        PurchaseService purchaseService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        if (string.IsNullOrWhiteSpace(req.SupplierId.ToString()) || req.SupplierId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Supplier ID is required.");

        if (req.WarehouseId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Warehouse ID is required.");

        var lines = req.Lines.Select(l =>
            new PurchaseService.CreateReceiptLineRequest(l.VariantId, l.Quantity, l.UnitCost)).ToList();

        var request = new PurchaseService.CreateReceiptRequest(
            req.SupplierId, req.WarehouseId, req.DocumentNumber, req.Notes, lines);

        var (receipt, errorCode, errorDetail) = await purchaseService.CreateAsync(
            request, currentUser.UserId.Value, ct);

        if (receipt is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.SupplierNotFound or ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound => 404,
                ErrorCodes.DocumentNumberExists => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Created($"/api/v1/purchases/{receipt.Id}", receipt);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdatePurchaseRequest req,
        PurchaseService purchaseService,
        CancellationToken ct)
    {
        var lines = req.Lines?.Select(l =>
            new PurchaseService.CreateReceiptLineRequest(l.VariantId, l.Quantity, l.UnitCost)).ToList();

        var request = new PurchaseService.UpdateReceiptRequest(
            req.SupplierId, req.WarehouseId, req.Notes, lines);

        var (receipt, errorCode, errorDetail) = await purchaseService.UpdateAsync(id, request, ct);

        if (receipt is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.PurchaseReceiptNotFound => 404,
                ErrorCodes.PurchaseReceiptAlreadyPosted => 409,
                ErrorCodes.SupplierNotFound or ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(receipt);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        PurchaseService purchaseService,
        CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await purchaseService.DeleteAsync(id, ct);

        if (!success)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.PurchaseReceiptNotFound => 404,
                ErrorCodes.PurchaseReceiptAlreadyPosted => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(new { message = "Purchase receipt deleted." });
    }

    private static async Task<IResult> PostAsync(
        Guid id,
        PurchaseService purchaseService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        var result = await purchaseService.PostAsync(id, currentUser.UserId.Value, ct);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.PurchaseReceiptNotFound => 404,
                ErrorCodes.PurchaseReceiptAlreadyPosted => 409,
                ErrorCodes.PostConcurrencyConflict => 409,
                ErrorCodes.StockNegativeNotAllowed => 422,
                ErrorCodes.VariantNotFound or ErrorCodes.WarehouseNotFound => 404,
                ErrorCodes.Conflict => 409,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }

        return Results.Ok(new { stockMovementId = result.StockMovementId });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──

    public sealed record CreatePurchaseRequest(
        Guid SupplierId,
        Guid WarehouseId,
        string? DocumentNumber,
        string? Notes,
        List<PurchaseLineRequest> Lines);

    public sealed record UpdatePurchaseRequest(
        Guid? SupplierId,
        Guid? WarehouseId,
        string? Notes,
        List<PurchaseLineRequest>? Lines);

    public sealed record PurchaseLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal UnitCost);
}

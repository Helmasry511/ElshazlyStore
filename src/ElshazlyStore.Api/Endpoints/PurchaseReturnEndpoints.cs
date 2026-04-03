using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

public static class PurchaseReturnEndpoints
{
    public static RouteGroupBuilder MapPurchaseReturnEndpoints(this RouteGroupBuilder group)
    {
        var returns = group.MapGroup("/purchase-returns").WithTags("Purchase Returns");

        returns.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewPurchaseReturns}");
        returns.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewPurchaseReturns}");
        returns.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchaseReturnCreate}");
        returns.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchaseReturnCreate}");
        returns.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchaseReturnCreate}");
        returns.MapPost("/{id:guid}/post", PostAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchaseReturnPost}");
        returns.MapPost("/{id:guid}/void", VoidAsync)
            .RequireAuthorization($"Permission:{Permissions.PurchaseReturnVoid}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        PurchaseReturnService service,
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
        return Results.Ok(new PagedResult<PurchaseReturnService.ReturnDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, PurchaseReturnService service, CancellationToken ct)
    {
        var dto = await service.GetByIdAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.PurchaseReturnNotFound, "Purchase return not found.");
        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreatePurchaseReturnRequest req,
        PurchaseReturnService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        if (req.SupplierId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Supplier ID is required.");
        if (req.WarehouseId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Warehouse ID is required.");
        if (req.Lines is null || req.Lines.Count == 0)
            return Problem(400, ErrorCodes.PurchaseReturnEmpty, "At least one line is required.");

        var lines = req.Lines.Select(l =>
            new PurchaseReturnService.ReturnLineRequest(
                l.VariantId, l.Quantity, l.UnitCost,
                l.ReasonCodeId, l.DispositionType, l.Notes)).ToList();
        var request = new PurchaseReturnService.CreateReturnRequest(
            req.SupplierId, req.WarehouseId, req.OriginalPurchaseReceiptId,
            req.ReturnDateUtc, req.Notes, lines);
        var (result, errorCode, errorDetail) = await service.CreateAsync(
            request, currentUser.UserId.Value, ct);

        if (result is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound
                    or ErrorCodes.SupplierNotFound or ErrorCodes.PurchaseReceiptNotFound
                    or ErrorCodes.ReasonCodeNotFound => 404,
                ErrorCodes.PurchaseReturnNumberExists => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }
        return Results.Created($"/api/v1/purchase-returns/{result.Id}", result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdatePurchaseReturnRequest req,
        PurchaseReturnService service,
        CancellationToken ct)
    {
        var lines = req.Lines?.Select(l =>
            new PurchaseReturnService.ReturnLineRequest(
                l.VariantId, l.Quantity, l.UnitCost,
                l.ReasonCodeId, l.DispositionType, l.Notes)).ToList();
        var request = new PurchaseReturnService.UpdateReturnRequest(
            req.SupplierId, req.WarehouseId,
            req.OriginalPurchaseReceiptId, req.ClearOriginalReceipt ?? false,
            req.Notes, lines);
        var (result, errorCode, errorDetail) = await service.UpdateAsync(id, request, ct);

        if (result is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.PurchaseReturnNotFound => 404,
                ErrorCodes.PurchaseReturnAlreadyPosted => 409,
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound
                    or ErrorCodes.SupplierNotFound or ErrorCodes.PurchaseReceiptNotFound
                    or ErrorCodes.ReasonCodeNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteAsync(Guid id, PurchaseReturnService service, CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await service.DeleteAsync(id, ct);
        if (!success)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.PurchaseReturnNotFound => 404,
                ErrorCodes.PurchaseReturnAlreadyPosted => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }
        return Results.Ok(new { message = "Purchase return deleted." });
    }

    private static async Task<IResult> PostAsync(
        Guid id, PurchaseReturnService service, ICurrentUserService currentUser, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        var result = await service.PostAsync(id, currentUser.UserId.Value, ct);
        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.PurchaseReturnNotFound => 404,
                ErrorCodes.PurchaseReturnAlreadyPosted or ErrorCodes.PostAlreadyPosted => 409,
                ErrorCodes.PostConcurrencyConflict => 409,
                ErrorCodes.PurchaseReturnAlreadyVoided => 409,
                ErrorCodes.StockNegativeNotAllowed => 422,
                ErrorCodes.ReturnQtyExceedsReceived => 422,
                ErrorCodes.ReasonCodeInactive => 400,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }
        return Results.Ok(new { stockMovementId = result.StockMovementId });
    }

    private static async Task<IResult> VoidAsync(
        Guid id, PurchaseReturnService service, ICurrentUserService currentUser, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        var result = await service.VoidAsync(id, currentUser.UserId.Value, ct);
        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.PurchaseReturnNotFound => 404,
                ErrorCodes.PurchaseReturnAlreadyVoided => 409,
                ErrorCodes.PurchaseReturnVoidNotAllowedAfterPost => 409,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }
        return Results.Ok(new { message = "Purchase return voided." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──
    public sealed record CreatePurchaseReturnRequest(
        Guid SupplierId, Guid WarehouseId, Guid? OriginalPurchaseReceiptId,
        DateTime? ReturnDateUtc, string? Notes, List<PurchaseReturnLineRequest> Lines);
    public sealed record UpdatePurchaseReturnRequest(
        Guid? SupplierId, Guid? WarehouseId,
        Guid? OriginalPurchaseReceiptId, bool? ClearOriginalReceipt,
        string? Notes, List<PurchaseReturnLineRequest>? Lines);
    public sealed record PurchaseReturnLineRequest(
        Guid VariantId, decimal Quantity, decimal UnitCost,
        Guid ReasonCodeId, DispositionType DispositionType, string? Notes);
}

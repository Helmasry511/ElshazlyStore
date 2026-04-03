using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Sales return CRUD, posting, and voiding endpoints.
/// Posting creates a SaleReturnReceipt stock movement (stock in)
/// based on disposition, and a CreditNote ledger entry for AR.
/// </summary>
public static class SalesReturnEndpoints
{
    public static RouteGroupBuilder MapSalesReturnEndpoints(this RouteGroupBuilder group)
    {
        var returns = group.MapGroup("/sales-returns").WithTags("Sales Returns");

        returns.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewSalesReturns}");
        returns.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.ViewSalesReturns}");
        returns.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesReturnCreate}");
        returns.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesReturnCreate}");
        returns.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesReturnCreate}");
        returns.MapPost("/{id:guid}/post", PostAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesReturnPost}");
        returns.MapPost("/{id:guid}/void", VoidAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesReturnVoid}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        SalesReturnService service,
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
        return Results.Ok(new PagedResult<SalesReturnService.ReturnDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        SalesReturnService service,
        CancellationToken ct)
    {
        var dto = await service.GetByIdAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.SalesReturnNotFound, "Sales return not found.");

        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateSalesReturnRequest req,
        SalesReturnService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        if (req.WarehouseId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Warehouse ID is required.");

        if (req.Lines is null || req.Lines.Count == 0)
            return Problem(400, ErrorCodes.SalesReturnEmpty, "At least one line is required.");

        var lines = req.Lines.Select(l =>
            new SalesReturnService.ReturnLineRequest(
                l.VariantId, l.Quantity, l.UnitPrice,
                l.ReasonCodeId, l.DispositionType, l.Notes)).ToList();

        var request = new SalesReturnService.CreateReturnRequest(
            req.WarehouseId, req.CustomerId, req.OriginalSalesInvoiceId,
            req.ReturnDateUtc, req.Notes, lines);

        var (result, errorCode, errorDetail) = await service.CreateAsync(
            request, currentUser.UserId.Value, ct);

        if (result is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound
                    or ErrorCodes.CustomerNotFound or ErrorCodes.SalesInvoiceNotFound
                    or ErrorCodes.ReasonCodeNotFound => 404,
                ErrorCodes.ReturnNumberExists => 409,
                ErrorCodes.SalesReturnDispositionNotAllowed => 400,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Created($"/api/v1/sales-returns/{result.Id}", result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateSalesReturnRequest req,
        SalesReturnService service,
        CancellationToken ct)
    {
        var lines = req.Lines?.Select(l =>
            new SalesReturnService.ReturnLineRequest(
                l.VariantId, l.Quantity, l.UnitPrice,
                l.ReasonCodeId, l.DispositionType, l.Notes)).ToList();

        var request = new SalesReturnService.UpdateReturnRequest(
            req.WarehouseId, req.CustomerId, req.ClearCustomer ?? false,
            req.OriginalSalesInvoiceId, req.ClearOriginalInvoice ?? false,
            req.Notes, lines);

        var (result, errorCode, errorDetail) = await service.UpdateAsync(id, request, ct);

        if (result is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.SalesReturnNotFound => 404,
                ErrorCodes.SalesReturnAlreadyPosted => 409,
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound
                    or ErrorCodes.CustomerNotFound or ErrorCodes.SalesInvoiceNotFound
                    or ErrorCodes.ReasonCodeNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        SalesReturnService service,
        CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await service.DeleteAsync(id, ct);

        if (!success)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.SalesReturnNotFound => 404,
                ErrorCodes.SalesReturnAlreadyPosted => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(new { message = "Sales return deleted." });
    }

    private static async Task<IResult> PostAsync(
        Guid id,
        SalesReturnService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        var result = await service.PostAsync(id, currentUser.UserId.Value, ct);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.SalesReturnNotFound => 404,
                ErrorCodes.SalesReturnAlreadyPosted or ErrorCodes.PostAlreadyPosted => 409,
                ErrorCodes.PostConcurrencyConflict => 409,
                ErrorCodes.SalesReturnAlreadyVoided => 409,
                ErrorCodes.StockNegativeNotAllowed => 422,
                ErrorCodes.ReturnQtyExceedsSold => 422,
                ErrorCodes.SalesReturnDispositionNotAllowed => 400,
                ErrorCodes.ReasonCodeInactive => 400,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }

        return Results.Ok(new { stockMovementId = result.StockMovementId });
    }

    private static async Task<IResult> VoidAsync(
        Guid id,
        SalesReturnService service,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        var result = await service.VoidAsync(id, currentUser.UserId.Value, ct);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.SalesReturnNotFound => 404,
                ErrorCodes.SalesReturnAlreadyVoided => 409,
                ErrorCodes.SalesReturnVoidNotAllowedAfterPost => 409,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }

        return Results.Ok(new { message = "Sales return voided." });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──

    public sealed record CreateSalesReturnRequest(
        Guid WarehouseId,
        Guid? CustomerId,
        Guid? OriginalSalesInvoiceId,
        DateTime? ReturnDateUtc,
        string? Notes,
        List<SalesReturnLineRequest> Lines);

    public sealed record UpdateSalesReturnRequest(
        Guid? WarehouseId,
        Guid? CustomerId,
        bool? ClearCustomer,
        Guid? OriginalSalesInvoiceId,
        bool? ClearOriginalInvoice,
        string? Notes,
        List<SalesReturnLineRequest>? Lines);

    public sealed record SalesReturnLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal UnitPrice,
        Guid ReasonCodeId,
        DispositionType DispositionType,
        string? Notes);
}

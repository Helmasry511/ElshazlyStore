using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Sales invoice CRUD, posting, and POS search endpoints.
/// Posting creates a SaleIssue stock movement (stock out).
/// </summary>
public static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this RouteGroupBuilder group)
    {
        var sales = group.MapGroup("/sales").WithTags("Sales");

        sales.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesRead}");
        sales.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesRead}");
        sales.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesWrite}");
        sales.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesWrite}");
        sales.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesWrite}");
        sales.MapPost("/{id:guid}/post", PostAsync)
            .RequireAuthorization($"Permission:{Permissions.SalesPost}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        SalesService salesService,
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

        var (items, totalCount) = await salesService.ListAsync(q, page, pageSize, sort, includeTotal, ct);
        return Results.Ok(new PagedResult<SalesService.InvoiceDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        SalesService salesService,
        CancellationToken ct)
    {
        var dto = await salesService.GetByIdAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.SalesInvoiceNotFound, "Sales invoice not found.");

        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateSalesInvoiceRequest req,
        SalesService salesService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        if (req.WarehouseId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "Warehouse ID is required.");

        if (req.Lines is null || req.Lines.Count == 0)
            return Problem(400, ErrorCodes.SalesInvoiceEmpty, "At least one line is required.");

        var lines = req.Lines.Select(l =>
            new SalesService.InvoiceLineRequest(l.VariantId, l.Quantity, l.UnitPrice, l.DiscountAmount)).ToList();

        var request = new SalesService.CreateInvoiceRequest(
            req.WarehouseId, req.CustomerId, req.InvoiceDateUtc, req.Notes, lines);

        var (invoice, errorCode, errorDetail) = await salesService.CreateAsync(
            request, currentUser.UserId.Value, ct);

        if (invoice is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound or ErrorCodes.CustomerNotFound => 404,
                ErrorCodes.InvoiceNumberExists => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Created($"/api/v1/sales/{invoice.Id}", invoice);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateSalesInvoiceRequest req,
        SalesService salesService,
        CancellationToken ct)
    {
        var lines = req.Lines?.Select(l =>
            new SalesService.InvoiceLineRequest(l.VariantId, l.Quantity, l.UnitPrice, l.DiscountAmount)).ToList();

        var request = new SalesService.UpdateInvoiceRequest(
            req.WarehouseId, req.CustomerId, req.ClearCustomer ?? false, req.Notes, lines);

        var (invoice, errorCode, errorDetail) = await salesService.UpdateAsync(id, request, ct);

        if (invoice is null)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.SalesInvoiceNotFound => 404,
                ErrorCodes.SalesInvoiceAlreadyPosted => 409,
                ErrorCodes.WarehouseNotFound or ErrorCodes.VariantNotFound or ErrorCodes.CustomerNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(invoice);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        SalesService salesService,
        CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await salesService.DeleteAsync(id, ct);

        if (!success)
        {
            var statusCode = errorCode switch
            {
                ErrorCodes.SalesInvoiceNotFound => 404,
                ErrorCodes.SalesInvoiceAlreadyPosted => 409,
                _ => 400,
            };
            return Problem(statusCode, errorCode!, errorDetail!);
        }

        return Results.Ok(new { message = "Sales invoice deleted." });
    }

    private static async Task<IResult> PostAsync(
        Guid id,
        SalesService salesService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        var result = await salesService.PostAsync(id, currentUser.UserId.Value, ct);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.SalesInvoiceNotFound => 404,
                ErrorCodes.SalesInvoiceAlreadyPosted => 409,
                ErrorCodes.PostConcurrencyConflict => 409,
                ErrorCodes.StockNegativeNotAllowed => 422,
                ErrorCodes.VariantNotFound or ErrorCodes.WarehouseNotFound => 404,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }

        return Results.Ok(new
        {
            stockMovementId = result.StockMovementId,
        });
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──

    public sealed record CreateSalesInvoiceRequest(
        Guid WarehouseId,
        Guid? CustomerId,
        DateTime? InvoiceDateUtc,
        string? Notes,
        List<SalesInvoiceLineRequest> Lines);

    public sealed record UpdateSalesInvoiceRequest(
        Guid? WarehouseId,
        Guid? CustomerId,
        bool? ClearCustomer,
        string? Notes,
        List<SalesInvoiceLineRequest>? Lines);

    public sealed record SalesInvoiceLineRequest(
        Guid VariantId,
        decimal Quantity,
        decimal UnitPrice,
        decimal? DiscountAmount);
}

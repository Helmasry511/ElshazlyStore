using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Payment CRUD endpoints. Creating a payment posts a negative ledger entry.
/// </summary>
public static class PaymentEndpoints
{
    public static RouteGroupBuilder MapPaymentEndpoints(this RouteGroupBuilder group)
    {
        var payments = group.MapGroup("/payments").WithTags("Payments");

        payments.MapGet("/", ListAsync)
            .RequireAuthorization($"Permission:{Permissions.PaymentsRead}");
        payments.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization($"Permission:{Permissions.PaymentsRead}");
        payments.MapPost("/", CreateAsync)
            .RequireAuthorization($"Permission:{Permissions.PaymentsWrite}");

        return group;
    }

    private static async Task<IResult> ListAsync(
        AccountingService accountingService,
        [FromQuery] string? partyType,
        [FromQuery] Guid? partyId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        PartyType? pt = null;
        if (!string.IsNullOrWhiteSpace(partyType))
        {
            if (!TryParsePartyType(partyType, out var parsed))
                return Problem(400, ErrorCodes.InvalidPartyType, "Invalid party type. Use 'customer' or 'supplier'.");
            pt = parsed;
        }

        var (items, totalCount) = await accountingService.GetPaymentsAsync(pt, partyId, q, page, pageSize, includeTotal, ct);
        return Results.Ok(new PagedResult<AccountingService.PaymentDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        AccountingService accountingService,
        CancellationToken ct)
    {
        var dto = await accountingService.GetPaymentByIdAsync(id, ct);
        if (dto is null)
            return Problem(404, ErrorCodes.PaymentNotFound, "Payment not found.");

        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreatePaymentRequest req,
        AccountingService accountingService,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");

        if (req.Amount <= 0)
            return Problem(400, ErrorCodes.ValidationFailed, "Amount must be positive.");

        if (!TryParsePartyType(req.PartyType, out var partyType))
            return Problem(400, ErrorCodes.InvalidPartyType, "Invalid party type. Use 'customer' or 'supplier'.");

        if (!TryParsePaymentMethod(req.Method, out var method))
            return Problem(400, ErrorCodes.InvalidPaymentMethod, "Invalid payment method. Use Cash, InstaPay, EWallet, or Visa.");

        var request = new AccountingService.CreatePaymentRequest(
            partyType, req.PartyId, req.Amount, method,
            req.WalletName, req.Reference, req.RelatedInvoiceId, req.PaymentDateUtc);

        var result = await accountingService.CreatePaymentAsync(
            request, currentUser.UserId.Value, allowOverpay: false, ct);

        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                ErrorCodes.SalesInvoiceNotFound => 404,
                ErrorCodes.PartyNotFound => 404,
                ErrorCodes.OverpaymentNotAllowed => 422,
                ErrorCodes.WalletNameRequired => 400,
                _ => 400,
            };
            return Problem(statusCode, result.ErrorCode!, result.ErrorDetail!);
        }

        return Results.Created($"/api/v1/payments/{result.Payment!.Id}", result.Payment);
    }

    private static bool TryParsePartyType(string value, out PartyType result)
    {
        result = value.ToLowerInvariant() switch
        {
            "customer" => PartyType.Customer,
            "supplier" => PartyType.Supplier,
            _ => (PartyType)(-1),
        };
        return (int)result >= 0;
    }

    private static bool TryParsePaymentMethod(string value, out PaymentMethod result)
    {
        result = value.ToLowerInvariant() switch
        {
            "cash" => PaymentMethod.Cash,
            "instapay" => PaymentMethod.InstaPay,
            "ewallet" => PaymentMethod.EWallet,
            "visa" => PaymentMethod.Visa,
            _ => (PaymentMethod)(-1),
        };
        return (int)result >= 0;
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──

    public sealed record CreatePaymentRequest(
        string PartyType,
        Guid PartyId,
        decimal Amount,
        string Method,
        string? WalletName,
        string? Reference,
        Guid? RelatedInvoiceId,
        DateTime? PaymentDateUtc);
}

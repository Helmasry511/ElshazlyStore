using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// AR/AP accounting endpoints: balances, ledger entries, payments.
/// </summary>
public static class AccountingEndpoints
{
    public static RouteGroupBuilder MapAccountingEndpoints(this RouteGroupBuilder group)
    {
        var accounting = group.MapGroup("/accounting").WithTags("Accounting");

        // Balances
        accounting.MapGet("/balances/customers", GetCustomerBalancesAsync)
            .RequireAuthorization($"Permission:{Permissions.AccountingRead}");
        accounting.MapGet("/balances/suppliers", GetSupplierBalancesAsync)
            .RequireAuthorization($"Permission:{Permissions.AccountingRead}");
        accounting.MapGet("/balances/{partyType}/{partyId:guid}", GetPartyBalanceAsync)
            .RequireAuthorization($"Permission:{Permissions.AccountingRead}");

        // Ledger
        accounting.MapGet("/ledger/{partyType}/{partyId:guid}", GetLedgerAsync)
            .RequireAuthorization($"Permission:{Permissions.AccountingRead}");

        return group;
    }

    private static async Task<IResult> GetCustomerBalancesAsync(
        AccountingService accountingService,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await accountingService.GetBalancesAsync(
            PartyType.Customer, q, page, pageSize, includeTotal, ct);
        return Results.Ok(new PagedResult<AccountingService.PartyBalanceDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetSupplierBalancesAsync(
        AccountingService accountingService,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await accountingService.GetBalancesAsync(
            PartyType.Supplier, q, page, pageSize, includeTotal, ct);
        return Results.Ok(new PagedResult<AccountingService.PartyBalanceDto>(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetPartyBalanceAsync(
        string partyType,
        Guid partyId,
        AccountingService accountingService,
        CancellationToken ct)
    {
        if (!TryParsePartyType(partyType, out var pt))
            return Problem(400, ErrorCodes.InvalidPartyType, "Invalid party type. Use 'customer' or 'supplier'.");

        var outstanding = await accountingService.ComputeOutstandingAsync(pt, partyId, ct);
        return Results.Ok(new { partyId, partyType = pt.ToString(), outstanding });
    }

    private static async Task<IResult> GetLedgerAsync(
        string partyType,
        Guid partyId,
        AccountingService accountingService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        if (!TryParsePartyType(partyType, out var pt))
            return Problem(400, ErrorCodes.InvalidPartyType, "Invalid party type. Use 'customer' or 'supplier'.");

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await accountingService.GetLedgerAsync(pt, partyId, page, pageSize, includeTotal, ct);
        return Results.Ok(new PagedResult<AccountingService.LedgerEntryDto>(items, totalCount, page, pageSize));
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

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");
}

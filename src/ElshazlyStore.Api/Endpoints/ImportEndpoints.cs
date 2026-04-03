using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Import endpoints: preview and commit master data from CSV/XLSX.
/// ADMIN ONLY — requires IMPORT_MASTER_DATA permission.
/// Phase 7 adds opening_balances and payments imports requiring
/// IMPORT_OPENING_BALANCES and IMPORT_PAYMENTS permissions respectively.
/// </summary>
public static class ImportEndpoints
{
    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder group)
    {
        var imports = group.MapGroup("/imports/masterdata").WithTags("Import");

        imports.MapPost("/preview", PreviewAsync)
            .RequireAuthorization($"Permission:{Permissions.ImportMasterData}")
            .DisableAntiforgery();

        imports.MapPost("/commit", CommitAsync)
            .RequireAuthorization($"Permission:{Permissions.ImportMasterData}");

        // Phase 7 — Opening balances import
        var balanceImports = group.MapGroup("/imports/opening-balances").WithTags("Import");

        balanceImports.MapPost("/preview", PreviewOpeningBalancesAsync)
            .RequireAuthorization($"Permission:{Permissions.ImportOpeningBalances}")
            .DisableAntiforgery();

        balanceImports.MapPost("/commit", CommitOpeningBalancesAsync)
            .RequireAuthorization($"Permission:{Permissions.ImportOpeningBalances}");

        // Phase 7 — Payments import
        var paymentImports = group.MapGroup("/imports/payments").WithTags("Import");

        paymentImports.MapPost("/preview", PreviewPaymentsAsync)
            .RequireAuthorization($"Permission:{Permissions.ImportPayments}")
            .DisableAntiforgery();

        paymentImports.MapPost("/commit", CommitPaymentsAsync)
            .RequireAuthorization($"Permission:{Permissions.ImportPayments}");

        return group;
    }

    private static async Task<IResult> PreviewAsync(
        HttpRequest request,
        ImportService importService,
        ICurrentUserService currentUser,
        [FromQuery] string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return Problem(400, ErrorCodes.ValidationFailed, "Query parameter 'type' is required (Products, Customers, Suppliers).");

        var validTypes = new[] { "products", "customers", "suppliers" };
        if (!validTypes.Contains(type.ToLowerInvariant()))
            return Problem(400, ErrorCodes.ValidationFailed, $"Invalid type '{type}'. Must be one of: Products, Customers, Suppliers.");

        if (!request.HasFormContentType || request.Form.Files.Count == 0)
            return Problem(400, ErrorCodes.ValidationFailed, "A file must be uploaded as multipart/form-data.");

        var file = request.Form.Files[0];
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext is not ".csv" and not ".xlsx")
            return Problem(400, ErrorCodes.ValidationFailed, "Only .csv and .xlsx files are supported.");

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User ID is required.");

        using var stream = file.OpenReadStream();
        var result = await importService.PreviewAsync(stream, file.FileName, type, userId);

        return Results.Ok(result);
    }

    private static async Task<IResult> CommitAsync(
        [FromBody] CommitRequest req,
        ImportService importService,
        ICurrentUserService currentUser)
    {
        if (req.JobId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "JobId is required.");

        var userId = currentUser.UserId;
        var result = await importService.CommitAsync(req.JobId, userId);

        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "IMPORT_JOB_NOT_FOUND" => 404,
                "IMPORT_JOB_ALREADY_COMMITTED" => 409,
                _ => 400
            };
            return Problem(status, result.ErrorCode ?? ErrorCodes.ImportCommitFailed,
                result.ErrorMessage ?? "Import commit failed.");
        }

        return Results.Ok(result);
    }

    // ── Opening Balances Import ──

    private static async Task<IResult> PreviewOpeningBalancesAsync(
        HttpRequest request,
        ImportService importService,
        ICurrentUserService currentUser)
    {
        return await PreviewFileAsync(request, importService, currentUser, "opening_balances");
    }

    private static async Task<IResult> CommitOpeningBalancesAsync(
        [FromBody] CommitRequest req,
        ImportService importService,
        ICurrentUserService currentUser)
    {
        return await CommitFileAsync(req, importService, currentUser);
    }

    // ── Payments Import ──

    private static async Task<IResult> PreviewPaymentsAsync(
        HttpRequest request,
        ImportService importService,
        ICurrentUserService currentUser)
    {
        return await PreviewFileAsync(request, importService, currentUser, "payments");
    }

    private static async Task<IResult> CommitPaymentsAsync(
        [FromBody] CommitRequest req,
        ImportService importService,
        ICurrentUserService currentUser)
    {
        return await CommitFileAsync(req, importService, currentUser);
    }

    // ── Shared Helpers ──

    private static async Task<IResult> PreviewFileAsync(
        HttpRequest request, ImportService importService,
        ICurrentUserService currentUser, string importType)
    {
        if (!request.HasFormContentType || request.Form.Files.Count == 0)
            return Problem(400, ErrorCodes.ValidationFailed, "A file must be uploaded as multipart/form-data.");

        var file = request.Form.Files[0];
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext is not ".csv" and not ".xlsx")
            return Problem(400, ErrorCodes.ValidationFailed, "Only .csv and .xlsx files are supported.");

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User ID is required.");

        using var stream = file.OpenReadStream();
        var result = await importService.PreviewAsync(stream, file.FileName, importType, userId);

        return Results.Ok(result);
    }

    private static async Task<IResult> CommitFileAsync(
        CommitRequest req, ImportService importService, ICurrentUserService currentUser)
    {
        if (req.JobId == Guid.Empty)
            return Problem(400, ErrorCodes.ValidationFailed, "JobId is required.");

        var userId = currentUser.UserId;
        var result = await importService.CommitAsync(req.JobId, userId);

        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "IMPORT_JOB_NOT_FOUND" => 404,
                "IMPORT_JOB_ALREADY_COMMITTED" => 409,
                _ => 400
            };
            return Problem(status, result.ErrorCode ?? ErrorCodes.ImportCommitFailed,
                result.ErrorMessage ?? "Import commit failed.");
        }

        return Results.Ok(result);
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    public sealed record CommitRequest(Guid JobId);
}

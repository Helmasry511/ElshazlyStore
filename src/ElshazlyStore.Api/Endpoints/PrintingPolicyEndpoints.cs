using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElshazlyStore.Api.Endpoints;

/// <summary>
/// Endpoints for managing printing profiles, rules, and policy lookup.
/// All mutating operations require MANAGE_PRINTING_POLICY permission.
/// Policy lookup (GET by screenCode) also requires the permission.
/// </summary>
public static class PrintingPolicyEndpoints
{
    public static RouteGroupBuilder MapPrintingPolicyEndpoints(this RouteGroupBuilder group)
    {
        var profiles = group.MapGroup("/print-profiles").WithTags("Printing Policy");
        var policy = group.MapGroup("/print-policy").WithTags("Printing Policy");

        // ── Profiles ──
        profiles.MapGet("/", ListProfilesAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapGet("/{id:guid}", GetProfileAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapPost("/", CreateProfileAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapPut("/{id:guid}", UpdateProfileAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapDelete("/{id:guid}", DeleteProfileAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");

        // ── Rules (nested under profile) ──
        profiles.MapGet("/{profileId:guid}/rules", ListRulesAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapGet("/{profileId:guid}/rules/{ruleId:guid}", GetRuleAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapPost("/{profileId:guid}/rules", CreateRuleAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapPut("/{profileId:guid}/rules/{ruleId:guid}", UpdateRuleAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");
        profiles.MapDelete("/{profileId:guid}/rules/{ruleId:guid}", DeleteRuleAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");

        // ── Policy lookup by screen code ──
        policy.MapGet("/{screenCode}", GetPolicyAsync)
            .RequireAuthorization($"Permission:{Permissions.ManagePrintingPolicy}");

        return group;
    }

    // ── Profile handlers ──

    private static async Task<IResult> ListProfilesAsync(
        PrintingPolicyService svc,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await svc.ListProfilesAsync(q, page, pageSize, includeTotal, ct);
        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    private static async Task<IResult> GetProfileAsync(
        Guid id, PrintingPolicyService svc, CancellationToken ct)
    {
        var profile = await svc.GetProfileAsync(id, ct);
        if (profile is null)
            return Problem(404, ErrorCodes.PrintProfileNotFound, "Print profile not found.");
        return Results.Ok(profile);
    }

    private static async Task<IResult> CreateProfileAsync(
        [FromBody] CreateProfileRequest req,
        PrintingPolicyService svc,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        if (string.IsNullOrWhiteSpace(req.Name))
            return Problem(400, ErrorCodes.ValidationFailed, "Profile name is required.");

        var (result, errorCode, errorDetail) = await svc.CreateProfileAsync(
            req.Name, req.IsDefault, currentUser.UserId.Value, ct);

        if (result is null)
        {
            var status = errorCode switch
            {
                ErrorCodes.PrintProfileNameExists => 409,
                _ => 400,
            };
            return Problem(status, errorCode!, errorDetail!);
        }

        return Results.Created($"/api/v1/print-profiles/{result.Id}", result);
    }

    private static async Task<IResult> UpdateProfileAsync(
        Guid id,
        [FromBody] UpdateProfileRequest req,
        PrintingPolicyService svc,
        CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await svc.UpdateProfileAsync(
            id, req.Name, req.IsDefault, req.IsActive, ct);

        if (!success)
        {
            var status = errorCode switch
            {
                ErrorCodes.PrintProfileNotFound => 404,
                ErrorCodes.PrintProfileNameExists => 409,
                _ => 400,
            };
            return Problem(status, errorCode!, errorDetail!);
        }

        return Results.Ok(new { message = "Print profile updated." });
    }

    private static async Task<IResult> DeleteProfileAsync(
        Guid id, PrintingPolicyService svc, CancellationToken ct)
    {
        var (success, errorCode, errorDetail) = await svc.DeleteProfileAsync(id, ct);
        if (!success)
            return Problem(404, errorCode!, errorDetail!);
        return Results.Ok(new { message = "Print profile deleted." });
    }

    // ── Rule handlers ──

    private static async Task<IResult> ListRulesAsync(
        Guid profileId,
        PrintingPolicyService svc,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = true,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await svc.ListRulesAsync(profileId, q, page, pageSize, includeTotal, ct);
        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    private static async Task<IResult> GetRuleAsync(
        Guid profileId, Guid ruleId, PrintingPolicyService svc, CancellationToken ct)
    {
        var rule = await svc.GetRuleAsync(ruleId, ct);
        if (rule is null || rule.PrintProfileId != profileId)
            return Problem(404, ErrorCodes.PrintRuleNotFound, "Print rule not found.");
        return Results.Ok(rule);
    }

    private static async Task<IResult> CreateRuleAsync(
        Guid profileId,
        [FromBody] CreateRuleRequest req,
        PrintingPolicyService svc,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Problem(401, ErrorCodes.Unauthorized, "Unauthorized.");
        if (string.IsNullOrWhiteSpace(req.ScreenCode))
            return Problem(400, ErrorCodes.ValidationFailed, "Screen code is required.");

        var (result, errorCode, errorDetail) = await svc.CreateRuleAsync(
            profileId, req.ScreenCode, req.ConfigJson ?? "{}", req.Enabled, currentUser.UserId.Value, ct);

        if (result is null)
        {
            var status = errorCode switch
            {
                ErrorCodes.PrintProfileNotFound => 404,
                ErrorCodes.PrintRuleScreenExists => 409,
                _ => 400,
            };
            return Problem(status, errorCode!, errorDetail!);
        }

        return Results.Created($"/api/v1/print-profiles/{profileId}/rules/{result.Id}", result);
    }

    private static async Task<IResult> UpdateRuleAsync(
        Guid profileId, Guid ruleId,
        [FromBody] UpdateRuleRequest req,
        PrintingPolicyService svc,
        CancellationToken ct)
    {
        // Verify rule belongs to profile
        var existing = await svc.GetRuleAsync(ruleId, ct);
        if (existing is null || existing.PrintProfileId != profileId)
            return Problem(404, ErrorCodes.PrintRuleNotFound, "Print rule not found.");

        var (success, errorCode, errorDetail) = await svc.UpdateRuleAsync(
            ruleId, req.ScreenCode, req.ConfigJson, req.Enabled, ct);

        if (!success)
        {
            var status = errorCode switch
            {
                ErrorCodes.PrintRuleNotFound => 404,
                ErrorCodes.PrintRuleScreenExists => 409,
                _ => 400,
            };
            return Problem(status, errorCode!, errorDetail!);
        }

        return Results.Ok(new { message = "Print rule updated." });
    }

    private static async Task<IResult> DeleteRuleAsync(
        Guid profileId, Guid ruleId, PrintingPolicyService svc, CancellationToken ct)
    {
        var existing = await svc.GetRuleAsync(ruleId, ct);
        if (existing is null || existing.PrintProfileId != profileId)
            return Problem(404, ErrorCodes.PrintRuleNotFound, "Print rule not found.");

        var (success, errorCode, errorDetail) = await svc.DeleteRuleAsync(ruleId, ct);
        if (!success)
            return Problem(404, errorCode!, errorDetail!);
        return Results.Ok(new { message = "Print rule deleted." });
    }

    // ── Policy lookup ──

    private static async Task<IResult> GetPolicyAsync(
        string screenCode,
        PrintingPolicyService svc,
        [FromQuery] Guid? profileId,
        CancellationToken ct)
    {
        var rule = await svc.GetPolicyByScreenCodeAsync(screenCode, profileId, ct);
        if (rule is null)
            return Problem(404, ErrorCodes.PrintRuleNotFound,
                $"No active print rule found for screen '{screenCode}'.");
        return Results.Ok(rule);
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(detail: detail, title: code, statusCode: status,
            type: $"https://elshazlystore.local/errors/{code.ToLowerInvariant()}");

    // ── DTOs ──

    public sealed record CreateProfileRequest(string Name, bool IsDefault = false);
    public sealed record UpdateProfileRequest(string? Name, bool? IsDefault, bool? IsActive);
    public sealed record CreateRuleRequest(string ScreenCode, string? ConfigJson, bool Enabled = true);
    public sealed record UpdateRuleRequest(string? ScreenCode, string? ConfigJson, bool? Enabled);
}

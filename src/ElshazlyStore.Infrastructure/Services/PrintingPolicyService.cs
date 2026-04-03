using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Service for managing print profiles and print rules.
/// </summary>
public sealed class PrintingPolicyService
{
    private readonly AppDbContext _db;

    public PrintingPolicyService(AppDbContext db)
    {
        _db = db;
    }

    // ── Profiles ──

    public async Task<(List<PrintProfileDto> Items, int TotalCount)> ListProfilesAsync(
        string? q, int page, int pageSize, bool includeTotal = true, CancellationToken ct = default)
    {
        IQueryable<PrintProfile> query = _db.PrintProfiles;

        query = query.ApplySearch(_db.Database, q,
            p => p.Name);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PrintProfileDto(
                p.Id, p.Name, p.IsDefault, p.IsActive,
                p.CreatedAtUtc, p.UpdatedAtUtc, p.CreatedByUserId))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<PrintProfileDetailDto?> GetProfileAsync(Guid id, CancellationToken ct)
    {
        var profile = await _db.PrintProfiles
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (profile is null) return null;

        return MapToDetailDto(profile);
    }

    public async Task<(PrintProfileDetailDto? Result, string? ErrorCode, string? ErrorDetail)> CreateProfileAsync(
        string name, bool isDefault, Guid userId, CancellationToken ct)
    {
        if (await _db.PrintProfiles.AnyAsync(p => p.Name == name, ct))
            return (null, ErrorCodes.PrintProfileNameExists, $"Profile name '{name}' already exists.");

        var profile = new PrintProfile
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsDefault = isDefault,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        // If marking as default, unset other defaults
        if (isDefault)
            await UnsetOtherDefaultsAsync(profile.Id, ct);

        _db.PrintProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        return (MapToDetailDto(profile), null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> UpdateProfileAsync(
        Guid id, string? name, bool? isDefault, bool? isActive, CancellationToken ct)
    {
        var profile = await _db.PrintProfiles.FindAsync([id], ct);
        if (profile is null)
            return (false, ErrorCodes.PrintProfileNotFound, "Print profile not found.");

        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (false, ErrorCodes.ValidationFailed, "Profile name cannot be empty.");
            if (await _db.PrintProfiles.AnyAsync(p => p.Name == name && p.Id != id, ct))
                return (false, ErrorCodes.PrintProfileNameExists, $"Profile name '{name}' already exists.");
            profile.Name = name;
        }

        if (isDefault.HasValue)
        {
            profile.IsDefault = isDefault.Value;
            if (isDefault.Value)
                await UnsetOtherDefaultsAsync(id, ct);
        }

        if (isActive.HasValue)
            profile.IsActive = isActive.Value;

        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> DeleteProfileAsync(
        Guid id, CancellationToken ct)
    {
        var profile = await _db.PrintProfiles.FindAsync([id], ct);
        if (profile is null)
            return (false, ErrorCodes.PrintProfileNotFound, "Print profile not found.");

        _db.PrintProfiles.Remove(profile);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ── Rules ──

    public async Task<(List<PrintRuleDto> Items, int TotalCount)> ListRulesAsync(
        Guid profileId, string? q, int page, int pageSize, bool includeTotal = true, CancellationToken ct = default)
    {
        IQueryable<PrintRule> query = _db.PrintRules.Where(r => r.PrintProfileId == profileId);

        query = query.ApplySearch(_db.Database, q,
            r => r.ScreenCode);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;
        var items = await query
            .OrderBy(r => r.ScreenCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PrintRuleDto(
                r.Id, r.PrintProfileId, r.ScreenCode, r.ConfigJson,
                r.Enabled, r.CreatedAtUtc, r.UpdatedAtUtc, r.CreatedByUserId))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<PrintRuleDto?> GetRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.PrintRules.FindAsync([ruleId], ct);
        if (rule is null) return null;

        return new PrintRuleDto(rule.Id, rule.PrintProfileId, rule.ScreenCode,
            rule.ConfigJson, rule.Enabled, rule.CreatedAtUtc, rule.UpdatedAtUtc, rule.CreatedByUserId);
    }

    public async Task<(PrintRuleDto? Result, string? ErrorCode, string? ErrorDetail)> CreateRuleAsync(
        Guid profileId, string screenCode, string configJson, bool enabled, Guid userId, CancellationToken ct)
    {
        if (!await _db.PrintProfiles.AnyAsync(p => p.Id == profileId, ct))
            return (null, ErrorCodes.PrintProfileNotFound, "Print profile not found.");

        if (await _db.PrintRules.AnyAsync(r => r.PrintProfileId == profileId && r.ScreenCode == screenCode, ct))
            return (null, ErrorCodes.PrintRuleScreenExists,
                $"A rule for screen '{screenCode}' already exists in this profile.");

        var rule = new PrintRule
        {
            Id = Guid.NewGuid(),
            PrintProfileId = profileId,
            ScreenCode = screenCode,
            ConfigJson = configJson,
            Enabled = enabled,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        _db.PrintRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return (new PrintRuleDto(rule.Id, rule.PrintProfileId, rule.ScreenCode,
            rule.ConfigJson, rule.Enabled, rule.CreatedAtUtc, rule.UpdatedAtUtc, rule.CreatedByUserId), null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> UpdateRuleAsync(
        Guid ruleId, string? screenCode, string? configJson, bool? enabled, CancellationToken ct)
    {
        var rule = await _db.PrintRules.FindAsync([ruleId], ct);
        if (rule is null)
            return (false, ErrorCodes.PrintRuleNotFound, "Print rule not found.");

        if (screenCode is not null)
        {
            if (string.IsNullOrWhiteSpace(screenCode))
                return (false, ErrorCodes.ValidationFailed, "Screen code cannot be empty.");
            if (await _db.PrintRules.AnyAsync(r => r.PrintProfileId == rule.PrintProfileId
                && r.ScreenCode == screenCode && r.Id != ruleId, ct))
                return (false, ErrorCodes.PrintRuleScreenExists,
                    $"A rule for screen '{screenCode}' already exists in this profile.");
            rule.ScreenCode = screenCode;
        }

        if (configJson is not null)
            rule.ConfigJson = configJson;

        if (enabled.HasValue)
            rule.Enabled = enabled.Value;

        rule.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> DeleteRuleAsync(
        Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.PrintRules.FindAsync([ruleId], ct);
        if (rule is null)
            return (false, ErrorCodes.PrintRuleNotFound, "Print rule not found.");

        _db.PrintRules.Remove(rule);
        await _db.SaveChangesAsync(ct);

        return (true, null, null);
    }

    // ── Policy lookup ──

    /// <summary>
    /// Returns the active print rule for a given screenCode from the default profile,
    /// or from a specific profile if profileId is provided.
    /// The UI uses this to determine how to print for a specific screen.
    /// </summary>
    public async Task<PrintRuleDto?> GetPolicyByScreenCodeAsync(
        string screenCode, Guid? profileId, CancellationToken ct)
    {
        IQueryable<PrintRule> query = _db.PrintRules
            .Include(r => r.Profile)
            .Where(r => r.ScreenCode == screenCode && r.Enabled && r.Profile.IsActive);

        if (profileId.HasValue)
            query = query.Where(r => r.PrintProfileId == profileId.Value);
        else
            query = query.Where(r => r.Profile.IsDefault);

        var rule = await query.FirstOrDefaultAsync(ct);
        if (rule is null) return null;

        return new PrintRuleDto(rule.Id, rule.PrintProfileId, rule.ScreenCode,
            rule.ConfigJson, rule.Enabled, rule.CreatedAtUtc, rule.UpdatedAtUtc, rule.CreatedByUserId);
    }

    // ── Helpers ──

    private async Task UnsetOtherDefaultsAsync(Guid excludeId, CancellationToken ct)
    {
        var others = await _db.PrintProfiles
            .Where(p => p.IsDefault && p.Id != excludeId)
            .ToListAsync(ct);
        foreach (var p in others)
            p.IsDefault = false;
    }

    private static PrintProfileDetailDto MapToDetailDto(PrintProfile profile) =>
        new(profile.Id, profile.Name, profile.IsDefault, profile.IsActive,
            profile.CreatedAtUtc, profile.UpdatedAtUtc, profile.CreatedByUserId,
            profile.Rules.Select(r => new PrintRuleDto(
                r.Id, r.PrintProfileId, r.ScreenCode, r.ConfigJson,
                r.Enabled, r.CreatedAtUtc, r.UpdatedAtUtc, r.CreatedByUserId)).ToList());

    // ── DTOs ──

    public sealed record PrintProfileDto(
        Guid Id, string Name, bool IsDefault, bool IsActive,
        DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, Guid? CreatedByUserId);

    public sealed record PrintProfileDetailDto(
        Guid Id, string Name, bool IsDefault, bool IsActive,
        DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, Guid? CreatedByUserId,
        List<PrintRuleDto> Rules);

    public sealed record PrintRuleDto(
        Guid Id, Guid PrintProfileId, string ScreenCode, string ConfigJson,
        bool Enabled, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, Guid? CreatedByUserId);
}

using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Infrastructure.Extensions;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// Service for managing the global reason-code catalog.
/// Reasons are never hard-deleted; they are soft-disabled.
/// </summary>
public sealed class ReasonCodeService
{
    private readonly AppDbContext _db;

    public ReasonCodeService(AppDbContext db)
    {
        _db = db;
    }

    // ── List ──

    public async Task<(List<ReasonCodeDto> Items, int TotalCount)> ListAsync(
        ReasonCategory? category, bool? isActive, string? q,
        int page, int pageSize, bool includeTotal = true, CancellationToken ct = default)
    {
        IQueryable<ReasonCode> query = _db.ReasonCodes;

        if (category.HasValue)
            query = query.Where(r => r.Category == category.Value);

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        query = query.ApplySearch(_db.Database, q, r => r.Code, r => r.NameAr);

        var totalCount = includeTotal ? await query.CountAsync(ct) : -1;

        var items = await query
            .OrderBy(r => r.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReasonCodeDto(
                r.Id, r.Code, r.NameAr, r.Description,
                r.Category.ToString(), r.IsActive,
                r.RequiresManagerApproval, r.CreatedAtUtc, r.UpdatedAtUtc))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    // ── Get ──

    public async Task<ReasonCodeDto?> GetAsync(Guid id, CancellationToken ct)
    {
        return await _db.ReasonCodes
            .Where(r => r.Id == id)
            .Select(r => new ReasonCodeDto(
                r.Id, r.Code, r.NameAr, r.Description,
                r.Category.ToString(), r.IsActive,
                r.RequiresManagerApproval, r.CreatedAtUtc, r.UpdatedAtUtc))
            .FirstOrDefaultAsync(ct);
    }

    // ── Create ──

    public async Task<(ReasonCodeDto? Result, string? ErrorCode, string? ErrorDetail)> CreateAsync(
        string code, string nameAr, string? description,
        ReasonCategory category, bool requiresManagerApproval,
        Guid userId, CancellationToken ct)
    {
        if (await _db.ReasonCodes.AnyAsync(r => r.Code == code, ct))
            return (null, ErrorCodes.ReasonCodeAlreadyExists,
                $"Reason code '{code}' already exists.");

        var entity = new ReasonCode
        {
            Id = Guid.NewGuid(),
            Code = code,
            NameAr = nameAr,
            Description = description,
            Category = category,
            IsActive = true,
            RequiresManagerApproval = requiresManagerApproval,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        _db.ReasonCodes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (MapDto(entity), null, null);
    }

    // ── Update ──

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> UpdateAsync(
        Guid id, string? nameAr, string? description,
        ReasonCategory? category, bool? requiresManagerApproval,
        Guid userId, CancellationToken ct)
    {
        var entity = await _db.ReasonCodes.FindAsync([id], ct);
        if (entity is null)
            return (false, ErrorCodes.ReasonCodeNotFound, "Reason code not found.");

        if (nameAr is not null)
            entity.NameAr = nameAr;

        if (description is not null)
            entity.Description = description;

        if (category.HasValue)
            entity.Category = category.Value;

        if (requiresManagerApproval.HasValue)
            entity.RequiresManagerApproval = requiresManagerApproval.Value;

        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return (true, null, null);
    }

    // ── Disable (soft-delete) ──

    public async Task<(bool Success, string? ErrorCode, string? ErrorDetail)> DisableAsync(
        Guid id, Guid userId, CancellationToken ct)
    {
        var entity = await _db.ReasonCodes.FindAsync([id], ct);
        if (entity is null)
            return (false, ErrorCodes.ReasonCodeNotFound, "Reason code not found.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return (true, null, null);
    }

    private static ReasonCodeDto MapDto(ReasonCode r) =>
        new(r.Id, r.Code, r.NameAr, r.Description,
            r.Category.ToString(), r.IsActive,
            r.RequiresManagerApproval, r.CreatedAtUtc, r.UpdatedAtUtc);
}

// ── DTOs ──

public sealed record ReasonCodeDto(
    Guid Id,
    string Code,
    string NameAr,
    string? Description,
    string Category,
    bool IsActive,
    bool RequiresManagerApproval,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

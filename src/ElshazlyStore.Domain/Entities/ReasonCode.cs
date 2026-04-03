namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Admin-managed reason catalog entry.
/// Referenced by returns, stock dispositions, and ledger entries.
/// Once created, a reason is never hard-deleted — it can only be disabled.
/// </summary>
public sealed class ReasonCode
{
    public Guid Id { get; set; }

    /// <summary>Stable machine-readable code (unique, immutable once set).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Arabic display name.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Optional long description.</summary>
    public string? Description { get; set; }

    /// <summary>Usage category.</summary>
    public ReasonCategory Category { get; set; } = ReasonCategory.General;

    /// <summary>Whether this reason is available for new transactions.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>If true, using this reason requires manager approval.</summary>
    public bool RequiresManagerApproval { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

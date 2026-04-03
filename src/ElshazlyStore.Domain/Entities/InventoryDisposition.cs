namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Pre-sale disposition header for handling damaged, stolen, expired,
/// defective, or non-conforming inventory discovered BEFORE sale.
/// Status flow: Draft → Posted → Voided (optional).
/// Posting creates a StockMovement of type Disposition with movements
/// into special warehouses (Scrap, Quarantine, Rework) or write-offs.
/// </summary>
public sealed class InventoryDisposition
{
    public Guid Id { get; set; }

    /// <summary>Server-generated sequential number (DISP-NNNNNN). Immutable after creation.</summary>
    public string DispositionNumber { get; set; } = string.Empty;

    /// <summary>Disposition date (business date, may differ from CreatedAtUtc).</summary>
    public DateTime DispositionDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Source warehouse where the issue was discovered.</summary>
    public Guid WarehouseId { get; set; }

    public string? Notes { get; set; }

    public DispositionStatus Status { get; set; } = DispositionStatus.Draft;

    /// <summary>Non-null after posting. Links to the Disposition stock movement.</summary>
    public Guid? StockMovementId { get; set; }

    /// <summary>Optimistic concurrency token.</summary>
    public byte[] RowVersion { get; set; } = [];

    // ── Approval audit trail ──

    /// <summary>User who approved the disposition (required when any line has a reason that RequiresManagerApproval).</summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>When the disposition was approved.</summary>
    public DateTime? ApprovedAtUtc { get; set; }

    // ── Standard audit fields ──

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }

    // Navigation
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? PostedBy { get; set; }
    public User? ApprovedBy { get; set; }
    public StockMovement? StockMovement { get; set; }
    public ICollection<InventoryDispositionLine> Lines { get; set; } = [];
}

/// <summary>
/// Inventory disposition lifecycle status.
/// </summary>
public enum DispositionStatus
{
    Draft = 0,
    Posted = 1,
    Voided = 2,
}

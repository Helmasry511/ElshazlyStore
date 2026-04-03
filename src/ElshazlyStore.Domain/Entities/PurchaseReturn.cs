namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Purchase return header for returning goods to a supplier.
/// Status flow: Draft → Posted → Voided (optional).
/// Posting creates a StockMovement of type PurchaseReturnIssue (negative delta = stock out)
/// and reduces the supplier payable (DebitNote ledger entry).
/// </summary>
public sealed class PurchaseReturn
{
    public Guid Id { get; set; }

    /// <summary>Server-generated sequential return number (PRET-NNNNNN). Immutable after creation.</summary>
    public string ReturnNumber { get; set; } = string.Empty;

    /// <summary>Return date (business date, may differ from CreatedAtUtc).</summary>
    public DateTime ReturnDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Supplier receiving the returned goods.</summary>
    public Guid SupplierId { get; set; }

    /// <summary>Optional link to the original purchase receipt for qty validation.</summary>
    public Guid? OriginalPurchaseReceiptId { get; set; }

    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }

    public PurchaseReturnStatus Status { get; set; } = PurchaseReturnStatus.Draft;

    /// <summary>Non-null after posting. Links to the PurchaseReturnIssue stock movement.</summary>
    public Guid? StockMovementId { get; set; }

    /// <summary>Computed total = sum of line totals.</summary>
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public PurchaseReceipt? OriginalPurchaseReceipt { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? PostedBy { get; set; }
    public StockMovement? StockMovement { get; set; }
    public ICollection<PurchaseReturnLine> Lines { get; set; } = [];
}

/// <summary>
/// Purchase return lifecycle status.
/// </summary>
public enum PurchaseReturnStatus
{
    Draft = 0,
    Posted = 1,
    Voided = 2,
}

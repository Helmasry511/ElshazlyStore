namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Purchase receipt / invoice header. Links to a supplier and contains
/// one or more <see cref="PurchaseReceiptLine"/> entries.
/// Status flow: Draft → Posted (terminal).
/// Posting creates a StockMovement of type PurchaseReceipt.
/// </summary>
public sealed class PurchaseReceipt
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }
    public PurchaseReceiptStatus Status { get; set; } = PurchaseReceiptStatus.Draft;

    /// <summary>Non-null after posting. Links to the generated stock movement.</summary>
    public Guid? StockMovementId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public StockMovement? StockMovement { get; set; }
    public ICollection<PurchaseReceiptLine> Lines { get; set; } = [];
}

/// <summary>
/// Purchase receipt lifecycle status.
/// </summary>
public enum PurchaseReceiptStatus
{
    Draft = 0,
    Posted = 1,
}

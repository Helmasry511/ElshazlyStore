namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// A production batch that consumes raw materials and produces finished goods.
/// Status flow: Draft → Posted (terminal).
/// Posting creates two stock movements atomically:
///   - ProductionConsume (negative deltas on raw material variants)
///   - ProductionProduce (positive deltas on finished goods variants)
/// </summary>
public sealed class ProductionBatch
{
    public Guid Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }
    public ProductionBatchStatus Status { get; set; } = ProductionBatchStatus.Draft;

    /// <summary>StockMovement for consumption (negative). Non-null after posting.</summary>
    public Guid? ConsumeMovementId { get; set; }
    /// <summary>StockMovement for production (positive). Non-null after posting.</summary>
    public Guid? ProduceMovementId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Navigation
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public StockMovement? ConsumeMovement { get; set; }
    public StockMovement? ProduceMovement { get; set; }
    public ICollection<ProductionBatchLine> Lines { get; set; } = [];
}

/// <summary>
/// Production batch lifecycle status.
/// </summary>
public enum ProductionBatchStatus
{
    Draft = 0,
    Posted = 1,
}

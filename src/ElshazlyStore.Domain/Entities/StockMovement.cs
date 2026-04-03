namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Header for a stock movement batch. Immutable after posting.
/// Each movement contains one or more <see cref="StockMovementLine"/> entries.
/// </summary>
public sealed class StockMovement
{
    public Guid Id { get; set; }
    public MovementType Type { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }

    // Navigation
    public User CreatedBy { get; set; } = null!;
    public ICollection<StockMovementLine> Lines { get; set; } = [];
}

/// <summary>
/// Supported stock movement types.
/// </summary>
public enum MovementType
{
    OpeningBalance = 0,
    PurchaseReceipt = 1,
    SaleIssue = 2,
    Transfer = 3,
    Adjustment = 4,
    ProductionConsume = 5,
    ProductionProduce = 6,
    SaleReturnReceipt = 7,
    PurchaseReturnIssue = 8,
    Disposition = 9,
}

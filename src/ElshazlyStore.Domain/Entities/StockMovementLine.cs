namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Individual line in a stock movement. Immutable after posting.
/// quantityDelta is positive for inbound, negative for outbound.
/// For Transfer type, two lines are created: negative from source, positive to destination.
/// </summary>
public sealed class StockMovementLine
{
    public Guid Id { get; set; }
    public Guid StockMovementId { get; set; }
    public Guid VariantId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal QuantityDelta { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Reason { get; set; }

    // Navigation
    public StockMovement StockMovement { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}

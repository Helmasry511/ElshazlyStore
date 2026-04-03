namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Materialized stock balance per variant per warehouse.
/// Updated transactionally during stock movement posting.
/// Used for fast reads; the movement lines are the source of truth.
/// </summary>
public sealed class StockBalance
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ProductVariant Variant { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}

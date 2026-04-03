namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class PostStockMovementRequest
{
    public int Type { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public List<PostStockMovementLineRequest> Lines { get; set; } = [];
}

public sealed class PostStockMovementLineRequest
{
    public Guid VariantId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal QuantityDelta { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Reason { get; set; }
}

public sealed class PostStockMovementResponse
{
    public Guid MovementId { get; set; }
}

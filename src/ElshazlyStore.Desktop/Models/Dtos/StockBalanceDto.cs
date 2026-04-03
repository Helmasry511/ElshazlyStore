namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class StockBalanceDto
{
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}

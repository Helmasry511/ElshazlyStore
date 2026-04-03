namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class BarcodeLookupResult
{
    public string Barcode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal? RetailPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public bool IsActive { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCategory { get; set; }
}

namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Product variant with color, size, pricing, and a globally unique barcode.
/// Each variant belongs to exactly one parent <see cref="Product"/>.
/// </summary>
public sealed class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal? RetailPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Minimum stock quantity before triggering a low-stock alert. Null means no threshold.</summary>
    public decimal? LowStockThreshold { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public BarcodeReservation? BarcodeReservation { get; set; }
}

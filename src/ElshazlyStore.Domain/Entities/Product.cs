namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Parent product. Variants belong to exactly one product.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional default warehouse for this product (metadata only — no stock quantities).
    /// Used as guidance when creating/viewing variants.
    /// </summary>
    public Guid? DefaultWarehouseId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public Warehouse? DefaultWarehouse { get; set; }
}

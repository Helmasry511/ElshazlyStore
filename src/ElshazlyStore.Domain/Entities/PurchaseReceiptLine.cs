namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Individual line on a purchase receipt: variant (raw material), quantity, unit cost.
/// Immutable after the parent receipt is posted.
/// </summary>
public sealed class PurchaseReceiptLine
{
    public Guid Id { get; set; }
    public Guid PurchaseReceiptId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    // Navigation
    public PurchaseReceipt PurchaseReceipt { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}

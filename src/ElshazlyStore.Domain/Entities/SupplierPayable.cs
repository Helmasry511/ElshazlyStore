namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Placeholder for supplier payable (Accounts Payable).
/// Created when a purchase receipt is posted.
/// Full AR/AP handling will be implemented in a later phase.
/// </summary>
public sealed class SupplierPayable
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Guid PurchaseReceiptId { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public PurchaseReceipt PurchaseReceipt { get; set; } = null!;
}

namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Individual line on an inventory disposition: variant, qty, reason, disposition type.
/// Immutable after the parent disposition is posted.
/// </summary>
public sealed class InventoryDispositionLine
{
    public Guid Id { get; set; }
    public Guid InventoryDispositionId { get; set; }
    public Guid VariantId { get; set; }

    /// <summary>Quantity affected. Must be positive.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Why this stock is being disposed (damage, theft, defect, etc.).</summary>
    public Guid ReasonCodeId { get; set; }

    /// <summary>What happens to the stock: Scrap, Quarantine, WriteOff, Rework.</summary>
    public DispositionType DispositionType { get; set; }

    /// <summary>Optional notes for this line.</summary>
    public string? Notes { get; set; }

    // Navigation
    public InventoryDisposition InventoryDisposition { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public ReasonCode ReasonCode { get; set; } = null!;
}

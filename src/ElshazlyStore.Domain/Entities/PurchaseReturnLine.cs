namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Individual line on a purchase return: variant, qty, unit cost, reason, disposition.
/// Immutable after the parent return is posted.
/// LineTotal = Quantity * UnitCost.
/// </summary>
public sealed class PurchaseReturnLine
{
    public Guid Id { get; set; }
    public Guid PurchaseReturnId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>Computed: Quantity * UnitCost.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Why the goods are being returned to the supplier.</summary>
    public Guid ReasonCodeId { get; set; }

    /// <summary>What happens to the returned stock. RET 2 only allows ReturnToVendor.</summary>
    public DispositionType DispositionType { get; set; } = DispositionType.ReturnToVendor;

    /// <summary>Optional notes for this line.</summary>
    public string? Notes { get; set; }

    // Navigation
    public PurchaseReturn PurchaseReturn { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public ReasonCode ReasonCode { get; set; } = null!;
}

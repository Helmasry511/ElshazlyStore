namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Individual line on a sales return: variant, qty, unit price, reason, disposition.
/// Immutable after the parent return is posted.
/// LineTotal = Quantity * UnitPrice.
/// </summary>
public sealed class SalesReturnLine
{
    public Guid Id { get; set; }
    public Guid SalesReturnId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Computed: Quantity * UnitPrice.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Why the customer is returning this item.</summary>
    public Guid ReasonCodeId { get; set; }

    /// <summary>What happens to the returned stock.</summary>
    public DispositionType DispositionType { get; set; }

    /// <summary>Optional notes for this line.</summary>
    public string? Notes { get; set; }

    // Navigation
    public SalesReturn SalesReturn { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public ReasonCode ReasonCode { get; set; } = null!;
}

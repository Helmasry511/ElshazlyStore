namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Individual line on a sales invoice: variant, qty, unit price, optional discount.
/// Immutable after the parent invoice is posted.
/// LineTotal = (Quantity * UnitPrice) - DiscountAmount.
/// </summary>
public sealed class SalesInvoiceLine
{
    public Guid Id { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Flat discount amount on this line. Default 0.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Computed: (Quantity * UnitPrice) - DiscountAmount.</summary>
    public decimal LineTotal { get; set; }

    // Navigation
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}

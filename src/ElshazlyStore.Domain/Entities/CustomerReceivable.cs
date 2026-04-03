namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Placeholder for customer receivable (Accounts Receivable).
/// Created when a sales invoice is posted (if customer is specified).
/// Full AR handling will be implemented in a later phase.
/// </summary>
public sealed class CustomerReceivable
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Customer Customer { get; set; } = null!;
    public SalesInvoice SalesInvoice { get; set; } = null!;
}

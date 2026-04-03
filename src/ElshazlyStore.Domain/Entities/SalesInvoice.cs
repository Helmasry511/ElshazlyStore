namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Sales invoice header for POS (retail/wholesale).
/// Customer is optional — null means walk-in retail customer.
/// Status flow: Draft → Posted (terminal).
/// Posting creates a StockMovement of type SaleIssue.
/// </summary>
public sealed class SalesInvoice
{
    public Guid Id { get; set; }

    /// <summary>Server-generated sequential invoice number. Immutable after creation.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Invoice date (business date, may differ from CreatedAtUtc).</summary>
    public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional customer. Null = walk-in retail.</summary>
    public Guid? CustomerId { get; set; }

    public Guid WarehouseId { get; set; }
    public Guid CashierUserId { get; set; }
    public string? Notes { get; set; }

    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;

    /// <summary>Non-null after posting. Links to the SaleIssue stock movement.</summary>
    public Guid? StockMovementId { get; set; }

    /// <summary>Computed total = sum of line totals after discount.</summary>
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAtUtc { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public User Cashier { get; set; } = null!;
    public StockMovement? StockMovement { get; set; }
    public ICollection<SalesInvoiceLine> Lines { get; set; } = [];
}

/// <summary>
/// Sales invoice lifecycle status.
/// </summary>
public enum SalesInvoiceStatus
{
    Draft = 0,
    Posted = 1,
}

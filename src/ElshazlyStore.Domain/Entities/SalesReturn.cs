namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Sales return header for customer returns.
/// Customer is optional — null means walk-in retail customer.
/// Status flow: Draft → Posted → Voided (optional).
/// Posting creates a StockMovement of type SaleReturnReceipt.
/// </summary>
public sealed class SalesReturn
{
    public Guid Id { get; set; }

    /// <summary>Server-generated sequential return number. Immutable after creation.</summary>
    public string ReturnNumber { get; set; } = string.Empty;

    /// <summary>Return date (business date, may differ from CreatedAtUtc).</summary>
    public DateTime ReturnDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional customer. Null = walk-in retail.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Optional link to the original sales invoice for validation.</summary>
    public Guid? OriginalSalesInvoiceId { get; set; }

    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }

    public SalesReturnStatus Status { get; set; } = SalesReturnStatus.Draft;

    /// <summary>Non-null after posting. Links to the SaleReturnReceipt stock movement.</summary>
    public Guid? StockMovementId { get; set; }

    /// <summary>Computed total = sum of line totals.</summary>
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public SalesInvoice? OriginalSalesInvoice { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? PostedBy { get; set; }
    public StockMovement? StockMovement { get; set; }
    public ICollection<SalesReturnLine> Lines { get; set; } = [];
}

/// <summary>
/// Sales return lifecycle status.
/// </summary>
public enum SalesReturnStatus
{
    Draft = 0,
    Posted = 1,
    Voided = 2,
}

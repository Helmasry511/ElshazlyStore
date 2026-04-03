namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class SaleDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDateUtc { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid CashierUserId { get; set; }
    public string CashierUsername { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? StockMovementId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public List<SaleLineDto>? Lines { get; set; }
    public SalePaymentTraceDto? PaymentTrace { get; set; }

    public string StatusDisplay => Status switch
    {
        "Draft" => Localization.Strings.Status_Draft,
        "Posted" => Localization.Strings.Status_Posted,
        "Voided" => Localization.Strings.Status_Voided,
        _ => Status
    };

    public string CustomerNameDisplay => string.IsNullOrWhiteSpace(CustomerName)
        ? Localization.Strings.Sales_AnonymousCustomer
        : CustomerName!;
}

public sealed class SalePaymentTraceDto
{
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? WalletName { get; set; }
    public string? PaymentReference { get; set; }
    public int PaymentCount { get; set; }
    public bool IsOperationalOnly { get; set; }
    public string? Note { get; set; }
}

public sealed class SaleLineDto
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class CreateSaleRequest
{
    public Guid WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime? InvoiceDateUtc { get; set; }
    public string? Notes { get; set; }
    public List<SaleLineRequest>? Lines { get; set; }
}

public sealed class UpdateSaleRequest
{
    public Guid? WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public bool ClearCustomer { get; set; }
    public string? Notes { get; set; }
    public List<SaleLineRequest>? Lines { get; set; }
}

public sealed class SaleLineRequest
{
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
}
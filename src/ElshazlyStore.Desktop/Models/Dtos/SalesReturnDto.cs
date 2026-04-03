using System.Text.Json.Serialization;

namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class SalesReturnDto
{
    public Guid Id { get; set; }

    [JsonPropertyName("returnNumber")]
    public string? DocumentNumber { get; set; }

    public Guid? OriginalSalesInvoiceId { get; set; }
    public string? OriginalInvoiceNumber { get; set; }

    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    [JsonPropertyName("totalAmount")]
    public decimal Total { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public DateTime? ReturnDateUtc { get; set; }

    public List<SalesReturnLineDto>? Lines { get; set; }

    public string StatusDisplay => Status switch
    {
        "Draft" => Localization.Strings.Status_Draft,
        "Posted" => Localization.Strings.Status_Posted,
        "Voided" => Localization.Strings.Status_Voided,
        _ => Status
    };

    public string CustomerNameDisplay =>
        string.IsNullOrWhiteSpace(CustomerName)
            ? Localization.Strings.Sales_AnonymousCustomer
            : CustomerName!;
}

public sealed class SalesReturnLineDto
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }

    [JsonPropertyName("sku")]
    public string VariantSku { get; set; } = string.Empty;

    public string? ProductName { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ReasonCodeId { get; set; }

    [JsonPropertyName("reasonCodeNameAr")]
    public string? ReasonCodeName { get; set; }

    public string DispositionType { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public string DispositionDisplay => DispositionType switch
    {
        "ReturnToStock" => Localization.Strings.SalesReturn_DispositionReturnToStock,
        "Quarantine" => Localization.Strings.SalesReturn_DispositionQuarantine,
        _ => DispositionType
    };
}

public sealed class CreateSalesReturnRequest
{
    public Guid WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? OriginalSalesInvoiceId { get; set; }
    public DateTime? ReturnDateUtc { get; set; }
    public string? Notes { get; set; }
    public List<SalesReturnLineRequest>? Lines { get; set; }
}

public sealed class UpdateSalesReturnRequest
{
    public Guid? WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public bool? ClearCustomer { get; set; }
    public Guid? OriginalSalesInvoiceId { get; set; }
    public bool? ClearOriginalInvoice { get; set; }
    public string? Notes { get; set; }
    public List<SalesReturnLineRequest>? Lines { get; set; }
}

public sealed class SalesReturnLineRequest
{
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid ReasonCodeId { get; set; }
    public int DispositionType { get; set; }
    public string? Notes { get; set; }
}

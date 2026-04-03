using System.Text.Json.Serialization;

namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class PurchaseReturnDto
{
    public Guid Id { get; set; }
    [JsonPropertyName("returnNumber")]
    public string? DocumentNumber { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? OriginalPurchaseReceiptId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    [JsonPropertyName("totalAmount")]
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public DateTime? ReturnDateUtc { get; set; }
    public List<PurchaseReturnLineDto>? Lines { get; set; }

    public string StatusDisplay => Status switch
    {
        "Draft" => Localization.Strings.Status_Draft,
        "Posted" => Localization.Strings.Status_Posted,
        "Voided" => Localization.Strings.Status_Voided,
        _ => Status
    };
}

public sealed class PurchaseReturnLineDto
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }
    [JsonPropertyName("sku")]
    public string VariantSku { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ReasonCodeId { get; set; }
    [JsonPropertyName("reasonCodeNameAr")]
    public string? ReasonCodeName { get; set; }
    public string DispositionType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class CreatePurchaseReturnRequest
{
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? OriginalPurchaseReceiptId { get; set; }
    public DateTime? ReturnDateUtc { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseReturnLineRequest>? Lines { get; set; }
}

public sealed class UpdatePurchaseReturnRequest
{
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? OriginalPurchaseReceiptId { get; set; }
    public bool? ClearOriginalReceipt { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseReturnLineRequest>? Lines { get; set; }
}

public sealed class PurchaseReturnLineRequest
{
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public Guid ReasonCodeId { get; set; }
    public int DispositionType { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReasonCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    [JsonPropertyName("nameAr")]
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string DisplayText => IsActive
        ? (string.IsNullOrEmpty(Code) ? Name : $"{Code} — {Name}")
        : (string.IsNullOrEmpty(Code) ? $"{Name} (غير نشط)" : $"{Code} — {Name} (غير نشط)");
}

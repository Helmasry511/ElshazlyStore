using System.Text.Json.Serialization;

namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class PurchaseDto
{
    public Guid Id { get; set; }
    public string? DocumentNumber { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Total => Lines?.Sum(l => l.LineTotal) ?? 0m;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public List<PurchaseLineDto>? Lines { get; set; }

    /// <summary>Arabic status display.</summary>
    public string StatusDisplay => Status switch
    {
        "Draft" => Localization.Strings.Status_Draft,
        "Posted" => Localization.Strings.Status_Posted,
        "Voided" => Localization.Strings.Status_Voided,
        _ => Status
    };
}

public sealed class PurchaseLineDto
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
}

public sealed class CreatePurchaseRequest
{
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseLineRequest>? Lines { get; set; }
}

public sealed class UpdatePurchaseRequest
{
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseLineRequest>? Lines { get; set; }
}

public sealed class PurchaseLineRequest
{
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

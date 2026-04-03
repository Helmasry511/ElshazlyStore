using System.ComponentModel;

namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class StockLedgerEntryDto : INotifyPropertyChanged
{
    public Guid MovementId { get; set; }
    public int Type { get; set; }
    public string? Reference { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public string? PostedByUsername { get; set; }
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal QuantityDelta { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Reason { get; set; }

    // ── UI-only: resolved after page load ──
    private string _warehouseName = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public string WarehouseName
    {
        get => _warehouseName;
        set { if (_warehouseName != value) { _warehouseName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarehouseName))); } }
    }

    private string _productVariantName = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public string ProductVariantName
    {
        get => _productVariantName;
        set { if (_productVariantName != value) { _productVariantName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProductVariantName))); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Maps the integer MovementType to Arabic display string.
    /// </summary>
    public string TypeDisplay => Type switch
    {
        0 => "رصيد افتتاحي",
        1 => "استلام مشتريات",
        2 => "صرف مبيعات",
        3 => "تحويل",
        4 => "تسوية",
        5 => "استهلاك إنتاج",
        6 => "إنتاج",
        7 => "مرتجع مبيعات",
        8 => "مرتجع مشتريات",
        9 => "تصرف",
        _ => Type.ToString()
    };

    public decimal InQuantity => QuantityDelta > 0 ? QuantityDelta : 0;
    public decimal OutQuantity => QuantityDelta < 0 ? Math.Abs(QuantityDelta) : 0;
}

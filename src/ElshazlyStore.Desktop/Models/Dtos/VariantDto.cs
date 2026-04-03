using System.ComponentModel;

namespace ElshazlyStore.Desktop.Models.Dtos;

public sealed class VariantDto : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal? RetailPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public bool IsActive { get; set; }
    public string? Barcode { get; set; }
    /// <summary>Read-only — derived from parent Product.DefaultWarehouseId.</summary>
    public Guid? DefaultWarehouseId { get; set; }
    /// <summary>Read-only — derived from parent Product.DefaultWarehouse.Name.</summary>
    public string? DefaultWarehouseName { get; set; }

    // ── Stock quantity display (UI-only, populated from stock/balances) ──
    private string _quantityDisplay = "…";
    [System.Text.Json.Serialization.JsonIgnore]
    public string QuantityDisplay
    {
        get => _quantityDisplay;
        set
        {
            if (_quantityDisplay != value)
            {
                _quantityDisplay = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuantityDisplay)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class VariantListDto : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal? RetailPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public bool IsActive { get; set; }
    public string? Barcode { get; set; }
    /// <summary>Read-only — derived from parent Product.DefaultWarehouseId.</summary>
    public Guid? DefaultWarehouseId { get; set; }
    /// <summary>Read-only — derived from parent Product.DefaultWarehouse.Name.</summary>
    public string? DefaultWarehouseName { get; set; }

    // ── Stock quantity display (UI-only, populated from stock/balances) ──
    private string _quantityDisplay = "…";
    [System.Text.Json.Serialization.JsonIgnore]
    public string QuantityDisplay
    {
        get => _quantityDisplay;
        set
        {
            if (_quantityDisplay != value)
            {
                _quantityDisplay = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuantityDisplay)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class CreateVariantRequest
{
    public Guid ProductId { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal? RetailPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? LowStockThreshold { get; set; }
}

public sealed class UpdateVariantRequest
{
    public string? Sku { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal? RetailPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public bool? IsActive { get; set; }
    public decimal? LowStockThreshold { get; set; }
}

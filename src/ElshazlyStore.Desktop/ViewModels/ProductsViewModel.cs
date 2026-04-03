using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class ProductsViewModel : PagedListViewModelBase<ProductDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;

    public ProductsViewModel(ApiClient apiClient, IPermissionService permissionService, IMessageService messageService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        Title = Localization.Strings.Nav_Products;
        CanWrite = _permissionService.HasPermission(PermissionCodes.ProductsWrite);
        _selectedSearchMode = Localization.Strings.SearchMode_All;
    }

    [ObservableProperty]
    private bool _canWrite;

    // ── Search Modes ──

    public List<string> SearchModes { get; } =
    [
        Localization.Strings.SearchMode_All,
        Localization.Strings.SearchMode_Barcode,
        Localization.Strings.SearchMode_Sku,
        Localization.Strings.SearchMode_Name
    ];

    [ObservableProperty]
    private string _selectedSearchMode;

    // ── Barcode Lookup Result ──

    [ObservableProperty]
    private BarcodeLookupResult? _barcodeLookupResult;

    [ObservableProperty]
    private bool _hasBarcodeLookupResult;

    [ObservableProperty]
    private string _barcodeLookupMessage = string.Empty;

    // ── Detail / Form state ──

    [ObservableProperty]
    private bool _isDetailOpen;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private ProductDetailDto? _productDetail;

    // ── Form fields ──

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formDescription = string.Empty;

    [ObservableProperty]
    private string _formCategory = string.Empty;

    [ObservableProperty]
    private bool _formIsActive = true;

    [ObservableProperty]
    private string _formError = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isEditMode;

    private Guid? _editingId;

    // ── Default Warehouse Picker ──

    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty]
    private WarehouseDto? _selectedWarehouse;

    [ObservableProperty]
    private bool _isLoadingWarehouses;

    private Guid? _originalDefaultWarehouseId;

    protected override Task<ApiResult<PagedResponse<ProductDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/products", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<ProductDto>>(url);
    }

    [RelayCommand]
    private async Task OpenCreateAsync()
    {
        _editingId = null;
        IsEditMode = false;
        FormName = string.Empty;
        FormDescription = string.Empty;
        FormCategory = string.Empty;
        FormIsActive = true;
        FormError = string.Empty;
        SelectedWarehouse = null;
        _originalDefaultWarehouseId = null;
        await LoadWarehousesAsync();
        IsEditing = true;
        IsDetailOpen = false;
    }

    [RelayCommand]
    private async Task OpenEditAsync(ProductDto? product)
    {
        if (product is null) return;
        _editingId = product.Id;
        IsEditMode = true;
        FormName = product.Name;
        FormDescription = product.Description ?? string.Empty;
        FormCategory = product.Category ?? string.Empty;
        FormIsActive = product.IsActive;
        FormError = string.Empty;
        _originalDefaultWarehouseId = product.DefaultWarehouseId;
        await LoadWarehousesAsync();
        // Pre-select the current warehouse (if any)
        SelectedWarehouse = product.DefaultWarehouseId is not null
            ? Warehouses.FirstOrDefault(w => w.Id == product.DefaultWarehouseId.Value)
            : null;
        IsEditing = true;
        IsDetailOpen = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormError = Localization.Strings.Validation_NameRequired;
            return;
        }

        IsSaving = true;
        FormError = string.Empty;

        try
        {
            if (_editingId is null)
            {
                var body = new CreateProductRequest
                {
                    Name = FormName.Trim(),
                    Description = string.IsNullOrWhiteSpace(FormDescription) ? null : FormDescription.Trim(),
                    Category = string.IsNullOrWhiteSpace(FormCategory) ? null : FormCategory.Trim(),
                    DefaultWarehouseId = SelectedWarehouse?.Id
                };
                var result = await ApiClient.PostAsync<ProductDto>("/api/v1/products", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
            }
            else
            {
                // Determine DefaultWarehouseId for update:
                // null = no change, Guid.Empty = clear, valid GUID = set
                Guid? warehousePayload;
                var newId = SelectedWarehouse?.Id;
                if (newId == _originalDefaultWarehouseId)
                {
                    warehousePayload = null; // no change
                }
                else if (newId is null)
                {
                    warehousePayload = Guid.Empty; // clear
                }
                else
                {
                    warehousePayload = newId; // set new warehouse
                }

                var body = new UpdateProductRequest
                {
                    Name = FormName.Trim(),
                    Description = string.IsNullOrWhiteSpace(FormDescription) ? null : FormDescription.Trim(),
                    Category = string.IsNullOrWhiteSpace(FormCategory) ? null : FormCategory.Trim(),
                    IsActive = FormIsActive,
                    DefaultWarehouseId = warehousePayload
                };
                var result = await ApiClient.PutAsync<ProductDto>($"/api/v1/products/{_editingId}", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
            }

            IsEditing = false;
            await LoadPageAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    // ── Unified Search ──

    [RelayCommand]
    private async Task UnifiedSearchAsync()
    {
        BarcodeLookupMessage = string.Empty;
        HasBarcodeLookupResult = false;
        BarcodeLookupResult = null;

        if (SelectedSearchMode == Localization.Strings.SearchMode_Barcode)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;
            try
            {
                var result = await ApiClient.GetAsync<BarcodeLookupResult>(
                    $"/api/v1/barcodes/{Uri.EscapeDataString(SearchText.Trim())}");
                if (result.IsSuccess && result.Data is not null)
                {
                    BarcodeLookupResult = result.Data;
                    HasBarcodeLookupResult = true;
                }
                else
                {
                    BarcodeLookupMessage = result.ErrorMessage ?? Localization.Strings.Barcode_NotFound;
                }
            }
            catch
            {
                BarcodeLookupMessage = Localization.Strings.State_UnexpectedError;
            }
        }
        else
        {
            CurrentPage = 1;
            await LoadPageAsync();
        }
    }

    [RelayCommand]
    private async Task ClearUnifiedSearchAsync()
    {
        BarcodeLookupMessage = string.Empty;
        HasBarcodeLookupResult = false;
        BarcodeLookupResult = null;
        SearchText = string.Empty;
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(ProductDto? product)
    {
        if (product is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDelete))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/products/{product.Id}");
        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task ViewDetailsAsync(ProductDto? product)
    {
        if (product is null) return;
        SelectedProduct = product;
        IsEditing = false;

        var result = await ApiClient.GetAsync<ProductDetailDto>($"/api/v1/products/{product.Id}");
        if (result.IsSuccess && result.Data is not null)
        {
            ProductDetail = result.Data;
            IsDetailOpen = true;
            await LoadVariantQuantitiesAsync(result.Data);
        }
        else
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        IsDetailOpen = false;
        ProductDetail = null;
    }

    // ── Default Warehouse Helpers ──

    [RelayCommand]
    private void ClearWarehouseSelection()
    {
        SelectedWarehouse = null;
    }

    /// <summary>
    /// Always reloads warehouses from backend (no stale cache).
    /// Called on every modal open + via the refresh button.
    /// </summary>
    [RelayCommand]
    private async Task LoadWarehousesAsync()
    {
        IsLoadingWarehouses = true;
        try
        {
            var currentSelectionId = SelectedWarehouse?.Id;
            var result = await ApiClient.GetAsync<PagedResponse<WarehouseDto>>(
                "/api/v1/warehouses?page=1&pageSize=500");
            Warehouses.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                // Only show active warehouses in the dropdown
                foreach (var w in result.Data.Items.Where(w => w.IsActive))
                    Warehouses.Add(w);
            }
            // Restore selection if the warehouse still exists and is active
            if (currentSelectionId is not null)
                SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == currentSelectionId);
        }
        catch
        {
            // Silently ignore — dropdown will be empty
        }
        finally
        {
            IsLoadingWarehouses = false;
        }
    }

    // ── Scope B: Batched stock quantity loading for product detail variants ──

    private async Task LoadVariantQuantitiesAsync(ProductDetailDto detail)
    {
        if (detail.Variants.Count == 0) return;

        // Set loading placeholder
        foreach (var v in detail.Variants)
            v.QuantityDisplay = "…";

        // Build a map of variantId -> total quantity across all warehouses
        var qtyMap = new Dictionary<Guid, decimal>();
        var variantIds = detail.Variants.Select(v => v.Id).ToHashSet();

        // Fetch balances using product name as search query (narrows results)
        var url = $"/api/v1/stock/balances?page=1&pageSize=200&q={Uri.EscapeDataString(detail.Name)}";
        var result = await ApiClient.GetAsync<PagedResponse<StockBalanceDto>>(url);

        if (result.IsSuccess && result.Data is not null)
        {
            foreach (var b in result.Data.Items.Where(b => variantIds.Contains(b.VariantId)))
            {
                if (qtyMap.ContainsKey(b.VariantId))
                    qtyMap[b.VariantId] += b.Quantity;
                else
                    qtyMap[b.VariantId] = b.Quantity;
            }

            // Fetch remaining pages
            for (int page = 2; page <= result.Data.TotalPages; page++)
            {
                var nextUrl = $"/api/v1/stock/balances?page={page}&pageSize=200&q={Uri.EscapeDataString(detail.Name)}";
                var nextResult = await ApiClient.GetAsync<PagedResponse<StockBalanceDto>>(nextUrl);
                if (nextResult.IsSuccess && nextResult.Data is not null)
                {
                    foreach (var b in nextResult.Data.Items.Where(b => variantIds.Contains(b.VariantId)))
                    {
                        if (qtyMap.ContainsKey(b.VariantId))
                            qtyMap[b.VariantId] += b.Quantity;
                        else
                            qtyMap[b.VariantId] = b.Quantity;
                    }
                }
            }
        }

        // Apply quantities
        foreach (var v in detail.Variants)
            v.QuantityDisplay = qtyMap.TryGetValue(v.Id, out var qty) ? qty.ToString("N0") : "0";
    }
}

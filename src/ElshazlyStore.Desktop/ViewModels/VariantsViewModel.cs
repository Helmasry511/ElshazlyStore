using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class VariantsViewModel : PagedListViewModelBase<VariantListDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly IStockChangeNotifier _stockNotifier;
    private readonly INavigationService _navigationService;

    private static readonly WarehouseDto AllWarehousesSentinel = new()
    {
        Id = Guid.Empty,
        Name = Localization.Strings.Field_AllWarehouses,
        IsActive = true
    };

    public VariantsViewModel(ApiClient apiClient, IPermissionService permissionService, IMessageService messageService, IStockChangeNotifier stockNotifier, INavigationService navigationService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        _stockNotifier = stockNotifier;
        _navigationService = navigationService;
        Title = Localization.Strings.Nav_Variants;
        CanWrite = _permissionService.HasPermission(PermissionCodes.ProductsWrite);
        _selectedSearchMode = Localization.Strings.SearchMode_All;

        _stockNotifier.StockChanged += OnStockChanged;
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

    // ── Form state ──

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _formSku = string.Empty;

    [ObservableProperty]
    private string _formBarcode = string.Empty;

    [ObservableProperty]
    private string _formColor = string.Empty;

    [ObservableProperty]
    private string _formSize = string.Empty;

    [ObservableProperty]
    private string _formRetailPrice = string.Empty;

    [ObservableProperty]
    private string _formWholesalePrice = string.Empty;

    [ObservableProperty]
    private bool _formIsActive = true;

    [ObservableProperty]
    private string _formError = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    private Guid? _editingId;

    // ── Product Picker ──

    private Guid? _selectedProductId;

    [ObservableProperty]
    private string _selectedProductName = string.Empty;

    [ObservableProperty]
    private bool _isProductSelected;

    [ObservableProperty]
    private string _productSearchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchingProducts;

    [ObservableProperty]
    private bool _hasProductSearchResults;

    public ObservableCollection<ProductDto> ProductSearchResults { get; } = [];

    // ── Default Warehouse (read-only from selected product) ──

    [ObservableProperty]
    private string _formDefaultWarehouseName = string.Empty;

    // ── Barcode Lookup (unified search) ──

    [ObservableProperty]
    private string _selectedSearchMode;

    [ObservableProperty]
    private BarcodeLookupResult? _lookupResult;

    [ObservableProperty]
    private string _lookupMessage = string.Empty;

    [ObservableProperty]
    private bool _hasLookupResult;

    // ── Warehouse Filter (top-level) ──

    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty]
    private WarehouseDto? _selectedWarehouse;

    [ObservableProperty]
    private bool _warehousesLoaded;

    [ObservableProperty]
    private string _warehouseFilterModeLabel = Localization.Strings.Stock_ViewTotal;

    // ── Balance Details Modal ──

    [ObservableProperty]
    private bool _isShowingBalanceDetails;

    [ObservableProperty]
    private string _balanceDetailVariantName = string.Empty;

    public ObservableCollection<StockBalanceDto> BalanceDetailItems { get; } = [];

    private Guid? _balanceDetailVariantId;

    protected override Task<ApiResult<PagedResponse<VariantListDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/variants", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<VariantListDto>>(url);
    }

    // ── Product Picker Commands ──

    [RelayCommand]
    private async Task SearchProductsAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductSearchText))
        {
            ProductSearchResults.Clear();
            HasProductSearchResults = false;
            return;
        }

        IsSearchingProducts = true;
        try
        {
            var url = $"/api/v1/products?page=1&pageSize=10&q={Uri.EscapeDataString(ProductSearchText.Trim())}";
            var result = await ApiClient.GetAsync<PagedResponse<ProductDto>>(url);

            ProductSearchResults.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var p in result.Data.Items)
                    ProductSearchResults.Add(p);
            }
            HasProductSearchResults = ProductSearchResults.Count > 0;
        }
        finally
        {
            IsSearchingProducts = false;
        }
    }

    [RelayCommand]
    private void SelectProduct(ProductDto? product)
    {
        if (product is null) return;
        _selectedProductId = product.Id;
        SelectedProductName = product.Name;
        IsProductSelected = true;
        FormDefaultWarehouseName = product.DefaultWarehouseName
            ?? Localization.Strings.Field_DefaultWarehouseNotSet;
        ProductSearchText = string.Empty;
        ProductSearchResults.Clear();
        HasProductSearchResults = false;
    }

    [RelayCommand]
    private void ClearProductSelection()
    {
        _selectedProductId = null;
        SelectedProductName = string.Empty;
        IsProductSelected = false;
        FormDefaultWarehouseName = string.Empty;
        ProductSearchText = string.Empty;
        ProductSearchResults.Clear();
        HasProductSearchResults = false;
    }

    // ── CRUD Commands ──

    [RelayCommand]
    private void OpenCreate()
    {
        _editingId = null;
        IsEditMode = false;
        ClearProductSelection();
        FormSku = string.Empty;
        FormBarcode = string.Empty;
        FormColor = string.Empty;
        FormSize = string.Empty;
        FormRetailPrice = string.Empty;
        FormWholesalePrice = string.Empty;
        FormIsActive = true;
        FormError = string.Empty;
        FormDefaultWarehouseName = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void OpenEdit(VariantListDto? variant)
    {
        if (variant is null) return;
        _editingId = variant.Id;
        IsEditMode = true;
        // Product is read-only in edit mode (UpdateVariantRequest has no ProductId)
        _selectedProductId = variant.ProductId;
        SelectedProductName = variant.ProductName;
        IsProductSelected = true;
        FormDefaultWarehouseName = variant.DefaultWarehouseName
            ?? Localization.Strings.Field_DefaultWarehouseNotSet;
        ProductSearchText = string.Empty;
        ProductSearchResults.Clear();
        FormSku = variant.Sku;
        FormBarcode = variant.Barcode ?? string.Empty;
        FormColor = variant.Color ?? string.Empty;
        FormSize = variant.Size ?? string.Empty;
        FormRetailPrice = variant.RetailPrice?.ToString("F2") ?? string.Empty;
        FormWholesalePrice = variant.WholesalePrice?.ToString("F2") ?? string.Empty;
        FormIsActive = variant.IsActive;
        FormError = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // SKU is optional on CREATE (server generates if omitted).
        // SKU is still sent as-is on EDIT.
        if (IsEditMode && string.IsNullOrWhiteSpace(FormSku))
        {
            FormError = Localization.Strings.Validation_SkuRequired;
            return;
        }

        IsSaving = true;
        FormError = string.Empty;

        try
        {
            if (_editingId is null)
            {
                if (_selectedProductId is null)
                {
                    FormError = Localization.Strings.Validation_ProductRequired;
                    return;
                }
                var body = new CreateVariantRequest
                {
                    ProductId = _selectedProductId.Value,
                    Sku = string.IsNullOrWhiteSpace(FormSku) ? null : FormSku.Trim(),
                    Barcode = string.IsNullOrWhiteSpace(FormBarcode) ? null : FormBarcode.Trim(),
                    Color = string.IsNullOrWhiteSpace(FormColor) ? null : FormColor.Trim(),
                    Size = string.IsNullOrWhiteSpace(FormSize) ? null : FormSize.Trim(),
                    RetailPrice = decimal.TryParse(FormRetailPrice, out var rp) ? rp : null,
                    WholesalePrice = decimal.TryParse(FormWholesalePrice, out var wp) ? wp : null
                };
                var result = await ApiClient.PostAsync<VariantListDto>("/api/v1/variants", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }

                // Show toast with generated SKU/Barcode from server response
                if (result.Data is not null)
                {
                    var msg = string.Format(
                        Localization.Strings.Variant_CreatedSuccess,
                        result.Data.Sku,
                        result.Data.Barcode ?? "-");
                    _messageService.ShowInfo(msg);
                }
            }
            else
            {
                var body = new UpdateVariantRequest
                {
                    Sku = FormSku.Trim(),
                    Color = string.IsNullOrWhiteSpace(FormColor) ? null : FormColor.Trim(),
                    Size = string.IsNullOrWhiteSpace(FormSize) ? null : FormSize.Trim(),
                    RetailPrice = decimal.TryParse(FormRetailPrice, out var rp) ? rp : null,
                    WholesalePrice = decimal.TryParse(FormWholesalePrice, out var wp) ? wp : null,
                    IsActive = FormIsActive
                };
                var result = await ApiClient.PutAsync<VariantListDto>($"/api/v1/variants/{_editingId}", body);
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
        ProductSearchResults.Clear();
    }

    [RelayCommand]
    private async Task DeleteAsync(VariantListDto? variant)
    {
        if (variant is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDelete))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/variants/{variant.Id}");
        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        await LoadPageAsync();
    }

    // ── Unified Search ──

    [RelayCommand]
    private async Task UnifiedSearchAsync()
    {
        LookupMessage = string.Empty;
        HasLookupResult = false;
        LookupResult = null;

        if (SelectedSearchMode == Localization.Strings.SearchMode_Barcode)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;
            try
            {
                var result = await ApiClient.GetAsync<BarcodeLookupResult>(
                    $"/api/v1/barcodes/{Uri.EscapeDataString(SearchText.Trim())}");
                if (result.IsSuccess && result.Data is not null)
                {
                    LookupResult = result.Data;
                    HasLookupResult = true;
                }
                else
                {
                    LookupMessage = result.ErrorMessage ?? Localization.Strings.Barcode_NotFound;
                }
            }
            catch
            {
                LookupMessage = Localization.Strings.State_UnexpectedError;
            }
        }
        else if (SelectedSearchMode == Localization.Strings.SearchMode_Sku)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;
            try
            {
                var result = await ApiClient.GetAsync<VariantListDto>(
                    $"/api/v1/variants/by-sku/{Uri.EscapeDataString(SearchText.Trim())}");
                if (result.IsSuccess && result.Data is not null)
                {
                    LookupResult = new BarcodeLookupResult
                    {
                        ProductName = result.Data.ProductName,
                        Sku = result.Data.Sku,
                        Color = result.Data.Color,
                        RetailPrice = result.Data.RetailPrice,
                        Status = result.Data.IsActive ? Localization.Strings.Status_Active : Localization.Strings.Status_Inactive,
                        Barcode = result.Data.Barcode ?? string.Empty
                    };
                    HasLookupResult = true;
                }
                else
                {
                    LookupMessage = Localization.Strings.Search_NotFound;
                }
            }
            catch
            {
                LookupMessage = Localization.Strings.State_UnexpectedError;
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
        LookupMessage = string.Empty;
        HasLookupResult = false;
        LookupResult = null;
        SearchText = string.Empty;
        CurrentPage = 1;
        await LoadPageAsync();
    }

    // ── Scope A: Batched stock quantity loading (R6 — net all warehouses default) ──

    // Full balance cache: variantId → list of (warehouseId, warehouseCode, warehouseName, qty)
    private (DateTime FetchedAt, List<StockBalanceDto> Balances)? _allBalancesCache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    protected override async Task OnPageLoadedAsync()
    {
        // Set loading placeholder for all variants
        foreach (var v in Items)
            v.QuantityDisplay = "…";

        await LoadWarehousesIfNeededAsync();
        await HydrateQuantitiesAsync();
    }

    private async Task LoadWarehousesIfNeededAsync()
    {
        if (WarehousesLoaded) return;

        var result = await ApiClient.GetAsync<PagedResponse<WarehouseDto>>(
            "/api/v1/warehouses?page=1&pageSize=500");

        Warehouses.Clear();
        Warehouses.Add(AllWarehousesSentinel);
        if (result.IsSuccess && result.Data is not null)
        {
            foreach (var w in result.Data.Items.Where(w => w.IsActive))
                Warehouses.Add(w);
        }
        if (SelectedWarehouse is null)
            SelectedWarehouse = AllWarehousesSentinel;
        WarehousesLoaded = true;
    }

    private async Task<List<StockBalanceDto>> GetAllBalancesAsync()
    {
        // Check cache
        if (_allBalancesCache.HasValue &&
            DateTime.UtcNow - _allBalancesCache.Value.FetchedAt < CacheTtl)
        {
            return _allBalancesCache.Value.Balances;
        }

        // Fetch all balances (no warehouseId → all warehouses)
        var allBalances = new List<StockBalanceDto>();
        var url = "/api/v1/stock/balances?page=1&pageSize=200";
        var result = await ApiClient.GetAsync<PagedResponse<StockBalanceDto>>(url);

        if (result.IsSuccess && result.Data is not null)
        {
            allBalances.AddRange(result.Data.Items);

            var totalPages = result.Data.TotalPages;
            for (int page = 2; page <= totalPages; page++)
            {
                var nextUrl = $"/api/v1/stock/balances?page={page}&pageSize=200";
                var nextResult = await ApiClient.GetAsync<PagedResponse<StockBalanceDto>>(nextUrl);
                if (nextResult.IsSuccess && nextResult.Data is not null)
                    allBalances.AddRange(nextResult.Data.Items);
            }
        }

        _allBalancesCache = (DateTime.UtcNow, allBalances);
        return allBalances;
    }

    private async Task HydrateQuantitiesAsync()
    {
        var allBalances = await GetAllBalancesAsync();
        var isAllWarehouses = SelectedWarehouse is null || SelectedWarehouse.Id == Guid.Empty;

        foreach (var variant in Items)
        {
            if (isAllWarehouses)
            {
                // Net total across all warehouses
                var netQty = allBalances
                    .Where(b => b.VariantId == variant.Id)
                    .Sum(b => b.Quantity);
                variant.QuantityDisplay = netQty.ToString("N0");
            }
            else
            {
                // Specific warehouse only
                var warehouseQty = allBalances
                    .Where(b => b.VariantId == variant.Id && b.WarehouseId == SelectedWarehouse!.Id)
                    .Sum(b => b.Quantity);
                variant.QuantityDisplay = warehouseQty.ToString("N0");
            }
        }

        WarehouseFilterModeLabel = isAllWarehouses
            ? Localization.Strings.Stock_ViewTotal
            : Localization.Strings.Stock_ViewWarehouse;
    }

    // ── Warehouse Filter Commands ──

    [RelayCommand]
    private async Task FilterByWarehouseAsync()
    {
        // Refresh only quantity column — do not reload the variants list
        foreach (var v in Items)
            v.QuantityDisplay = "…";
        await HydrateQuantitiesAsync();
    }

    // ── Balance Details Modal (double-click) ──

    [RelayCommand]
    private async Task ShowBalanceDetailsAsync(VariantListDto? variant)
    {
        if (variant is null) return;

        _balanceDetailVariantId = variant.Id;
        BalanceDetailVariantName = $"{variant.ProductName}" +
            (!string.IsNullOrEmpty(variant.Color) ? $" ({variant.Color})" : "") +
            (!string.IsNullOrEmpty(variant.Size) ? $" [{variant.Size}]" : "");

        BalanceDetailItems.Clear();

        var allBalances = await GetAllBalancesAsync();
        var variantBalances = allBalances.Where(b => b.VariantId == variant.Id).ToList();

        foreach (var b in variantBalances)
            BalanceDetailItems.Add(b);

        IsShowingBalanceDetails = true;
    }

    [RelayCommand]
    private void CloseBalanceDetails()
    {
        IsShowingBalanceDetails = false;
        BalanceDetailItems.Clear();
        _balanceDetailVariantId = null;
    }

    [RelayCommand]
    private void NavigateToStockLedger()
    {
        IsShowingBalanceDetails = false;
        _navigationService.NavigateTo<StockLedgerViewModel>();
    }

    // ── StockChanged handler (auto-refresh after purchase/return/movement post) ──

    private async void OnStockChanged(object? sender, EventArgs e)
    {
        _allBalancesCache = null;

        // Re-hydrate quantities for the current page (UI-thread safe via dispatcher)
        if (Items.Count > 0)
            await HydrateQuantitiesAsync();
    }

    // ── Manual refresh quantities command ──

    [RelayCommand]
    private async Task RefreshQuantitiesAsync()
    {
        _allBalancesCache = null;
        if (Items.Count > 0)
        {
            foreach (var v in Items)
                v.QuantityDisplay = "…";
            await HydrateQuantitiesAsync();
        }
    }
}

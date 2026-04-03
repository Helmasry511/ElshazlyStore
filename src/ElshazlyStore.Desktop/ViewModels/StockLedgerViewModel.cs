using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class StockLedgerViewModel : PagedListViewModelBase<StockLedgerEntryDto>
{
    private CancellationTokenSource? _variantSearchCts;

    // Cache for resolved variant display names (variantId -> display)
    private readonly Dictionary<Guid, string> _variantNameCache = new();

    private static readonly WarehouseDto AllWarehousesSentinel = new()
    {
        Id = Guid.Empty,
        Name = Localization.Strings.Field_AllWarehouses,
        IsActive = true
    };

    public StockLedgerViewModel(ApiClient apiClient) : base(apiClient)
    {
        Title = Localization.Strings.Stock_LedgerTitle;
    }

    // ── Warehouse filter ──
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty]
    private WarehouseDto? _selectedWarehouse;

    // ── Variant picker ──
    [ObservableProperty]
    private string _variantSearchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchingVariants;

    [ObservableProperty]
    private bool _hasVariantSearchResults;

    public ObservableCollection<VariantListDto> VariantSearchResults { get; } = [];

    private Guid? _selectedVariantId;

    [ObservableProperty]
    private string _selectedVariantName = string.Empty;

    [ObservableProperty]
    private bool _isVariantSelected;

    /// <summary>True when no specific variant filter is selected (show product name column).</summary>
    [ObservableProperty]
    private bool _showProductColumn = true;

    // ── Date range filter ──
    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    /// <summary>True once user has clicked Apply — controls whether the DataGrid or the hint is shown.</summary>
    [ObservableProperty]
    private bool _filtersApplied;

    /// <summary>Debounced typeahead: triggers search 250ms after the user stops typing (min 2 chars).</summary>
    partial void OnVariantSearchTextChanged(string value)
    {
        _variantSearchCts?.Cancel();

        if (IsVariantSelected) return;

        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            VariantSearchResults.Clear();
            HasVariantSearchResults = false;
            return;
        }

        _variantSearchCts = new CancellationTokenSource();
        var token = _variantSearchCts.Token;

        _ = DebounceSearchVariantsAsync(value.Trim(), token);
    }

    private async Task DebounceSearchVariantsAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;
            await SearchVariantsCoreAsync(query, ct);
        }
        catch (TaskCanceledException) { /* expected on new keystroke */ }
    }

    protected override Task<ApiResult<PagedResponse<StockLedgerEntryDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = $"/api/v1/stock/ledger?page={page}&pageSize={pageSize}";

        if (_selectedVariantId is not null)
            url += $"&variantId={_selectedVariantId}";

        if (SelectedWarehouse is not null && SelectedWarehouse.Id != Guid.Empty)
            url += $"&warehouseId={SelectedWarehouse.Id}";

        if (DateFrom.HasValue)
            url += $"&from={DateFrom.Value:yyyy-MM-ddTHH:mm:ssZ}";

        if (DateTo.HasValue)
            url += $"&to={DateTo.Value:yyyy-MM-ddTHH:mm:ssZ}";

        return ApiClient.GetAsync<PagedResponse<StockLedgerEntryDto>>(url);
    }

    [RelayCommand]
    private async Task SearchVariantsAsync()
    {
        if (string.IsNullOrWhiteSpace(VariantSearchText) || VariantSearchText.Trim().Length < 2)
        {
            VariantSearchResults.Clear();
            HasVariantSearchResults = false;
            return;
        }

        _variantSearchCts?.Cancel();
        _variantSearchCts = new CancellationTokenSource();
        await SearchVariantsCoreAsync(VariantSearchText.Trim(), _variantSearchCts.Token);
    }

    private async Task SearchVariantsCoreAsync(string query, CancellationToken ct)
    {
        IsSearchingVariants = true;
        try
        {
            var url = $"/api/v1/variants?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
            var result = await ApiClient.GetAsync<PagedResponse<VariantListDto>>(url);

            if (ct.IsCancellationRequested) return;

            VariantSearchResults.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var v in result.Data.Items)
                    VariantSearchResults.Add(v);
            }
            HasVariantSearchResults = VariantSearchResults.Count > 0;
        }
        finally
        {
            IsSearchingVariants = false;
        }
    }

    [RelayCommand]
    private void SelectVariant(VariantListDto? variant)
    {
        if (variant is null) return;
        _selectedVariantId = variant.Id;

        // Build display: ProductName (Color/Size) — SKU
        var meta = BuildColorSizeMeta(variant.Color, variant.Size);
        SelectedVariantName = string.IsNullOrEmpty(meta)
            ? $"{variant.ProductName} — {variant.Sku}"
            : $"{variant.ProductName} ({meta}) — {variant.Sku}";

        IsVariantSelected = true;
        VariantSearchText = string.Empty;
        VariantSearchResults.Clear();
        HasVariantSearchResults = false;
    }

    [RelayCommand]
    private void ClearVariantSelection()
    {
        _selectedVariantId = null;
        SelectedVariantName = string.Empty;
        IsVariantSelected = false;
        VariantSearchText = string.Empty;
        VariantSearchResults.Clear();
        HasVariantSearchResults = false;
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        // Show product column only when no specific variant is selected
        ShowProductColumn = !IsVariantSelected;
        FiltersApplied = true;
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    private Task ClearFiltersAsync()
    {
        ClearVariantSelection();
        SelectedWarehouse = AllWarehousesSentinel;
        DateFrom = null;
        DateTo = null;
        FiltersApplied = false;
        CurrentPage = 1;
        Items.Clear();
        IsEmpty = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadWarehousesAsync()
    {
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
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await LoadWarehousesAsync();
    }

    /// <summary>
    /// After page loaded: map warehouse names from local list, batch-resolve variant names from API.
    /// </summary>
    protected override async Task OnPageLoadedAsync()
    {
        // Build warehouse lookup from already-loaded list
        var warehouseLookup = Warehouses
            .Where(w => w.Id != Guid.Empty)
            .ToDictionary(w => w.Id, w => w.Name);

        // Map warehouse names
        foreach (var entry in Items)
        {
            if (warehouseLookup.TryGetValue(entry.WarehouseId, out var whName))
                entry.WarehouseName = whName;
        }

        // Resolve variant display names (only when product column is shown)
        if (ShowProductColumn)
        {
            var unknownIds = Items
                .Select(e => e.VariantId)
                .Where(id => !_variantNameCache.ContainsKey(id))
                .Distinct()
                .ToList();

            // Batch-resolve unknown variant IDs
            foreach (var variantId in unknownIds)
            {
                var result = await ApiClient.GetAsync<VariantListDto>(
                    $"/api/v1/variants/{variantId}");
                if (result.IsSuccess && result.Data is not null)
                {
                    var v = result.Data;
                    var meta = BuildColorSizeMeta(v.Color, v.Size);
                    var display = string.IsNullOrEmpty(meta)
                        ? v.ProductName
                        : $"{v.ProductName} ({meta})";
                    _variantNameCache[variantId] = display;
                }
                else
                {
                    // Fallback to SKU
                    _variantNameCache[variantId] = string.Empty;
                }
            }

            // Apply resolved names to items
            foreach (var entry in Items)
            {
                if (_variantNameCache.TryGetValue(entry.VariantId, out var name) && !string.IsNullOrEmpty(name))
                    entry.ProductVariantName = name;
                else
                    entry.ProductVariantName = entry.Sku; // fallback
            }
        }
    }

    private static string BuildColorSizeMeta(string? color, string? size)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!);
        if (!string.IsNullOrWhiteSpace(size)) parts.Add(size!);
        return string.Join(" / ", parts);
    }
}

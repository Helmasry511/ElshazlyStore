using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class StockBalancesViewModel : PagedListViewModelBase<StockBalanceDto>
{
    private readonly INavigationService _navigationService;

    private static readonly WarehouseDto AllWarehousesSentinel = new()
    {
        Id = Guid.Empty,
        Name = Localization.Strings.Field_AllWarehouses,
        IsActive = true
    };

    public StockBalancesViewModel(ApiClient apiClient, INavigationService navigationService) : base(apiClient)
    {
        _navigationService = navigationService;
        Title = Localization.Strings.Stock_BalancesTitle;
    }

    // ── Warehouse filter ──
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty]
    private WarehouseDto? _selectedWarehouse;

    [ObservableProperty]
    private bool _warehousesLoaded;

    /// <summary>True when results are empty AND user has no active search/filter — indicates no movements exist at all.</summary>
    [ObservableProperty]
    private bool _isEmptyNoSearch;

    protected override Task<ApiResult<PagedResponse<StockBalanceDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = $"/api/v1/stock/balances?page={page}&pageSize={pageSize}";

        if (SelectedWarehouse is not null && SelectedWarehouse.Id != Guid.Empty)
            url += $"&warehouseId={SelectedWarehouse.Id}";

        if (!string.IsNullOrWhiteSpace(search))
            url += $"&q={Uri.EscapeDataString(search)}";

        if (!string.IsNullOrWhiteSpace(sort))
            url += $"&sort={Uri.EscapeDataString(sort)}";

        return ApiClient.GetAsync<PagedResponse<StockBalanceDto>>(url);
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
        WarehousesLoaded = true;
    }

    [RelayCommand]
    private async Task FilterByWarehouseAsync()
    {
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task ClearWarehouseFilterAsync()
    {
        SelectedWarehouse = AllWarehousesSentinel;
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await LoadWarehousesAsync();
        await LoadPageAsync();
    }

    protected override Task OnPageLoadedAsync()
    {
        IsEmptyNoSearch = IsEmpty
            && string.IsNullOrWhiteSpace(SearchText)
            && (SelectedWarehouse is null || SelectedWarehouse.Id == Guid.Empty);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void NavigateToMovements()
    {
        _navigationService.NavigateTo<StockMovementsViewModel>();
    }
}

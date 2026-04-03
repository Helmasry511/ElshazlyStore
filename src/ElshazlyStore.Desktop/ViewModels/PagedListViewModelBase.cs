using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

/// <summary>
/// Abstract base ViewModel for paged list screens with search, sort, and refresh.
/// Concrete derived classes provide <see cref="FetchPageAsync"/> to call the API.
/// </summary>
public abstract partial class PagedListViewModelBase<T> : ViewModelBase
{
    protected readonly ApiClient ApiClient;
    private CancellationTokenSource? _overlayDelayCts;
    private const int OverlayDelayMs = 180;

    protected PagedListViewModelBase(ApiClient apiClient)
    {
        ApiClient = apiClient;
    }

    // ═══ Observable State ═══

    /// <summary>The current page items.</summary>
    public ObservableCollection<T> Items { get; } = [];

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 25;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _sortColumn = string.Empty;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingOverlayVisible;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Formatted paging info for the UI, e.g. "صفحة 1 من 4 (100 عنصر)"</summary>
    [ObservableProperty]
    private string _pagingInfo = string.Empty;

    // ═══ Commands ═══

    [RelayCommand]
    private Task LoadAsync() => LoadPageAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadPageAsync();

    [RelayCommand]
    private Task SearchAsync()
    {
        CurrentPage = 1;
        return LoadPageAsync();
    }

    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        CurrentPage = 1;
        return LoadPageAsync();
    }

    [RelayCommand]
    private Task FirstPageAsync()
    {
        if (CurrentPage > 1) { CurrentPage = 1; return LoadPageAsync(); }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task PreviousPageAsync()
    {
        if (CurrentPage > 1) { CurrentPage--; return LoadPageAsync(); }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task NextPageAsync()
    {
        if (CurrentPage < TotalPages) { CurrentPage++; return LoadPageAsync(); }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task LastPageAsync()
    {
        if (CurrentPage < TotalPages) { CurrentPage = TotalPages; return LoadPageAsync(); }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SortByAsync(string column)
    {
        if (SortColumn == column)
            SortDescending = !SortDescending;
        else
        {
            SortColumn = column;
            SortDescending = false;
        }
        CurrentPage = 1;
        return LoadPageAsync();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        _overlayDelayCts?.Cancel();

        if (!value)
        {
            IsLoadingOverlayVisible = false;
            return;
        }

        _overlayDelayCts = new CancellationTokenSource();
        _ = DelayOverlayAsync(_overlayDelayCts.Token);
    }

    // ═══ Core Load ═══

    /// <summary>
    /// Loads the current page from the API. Manages busy/error/empty states.
    /// </summary>
    protected async Task LoadPageAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var sortParam = string.IsNullOrWhiteSpace(SortColumn)
                ? null
                : SortDescending ? $"{SortColumn}_desc" : SortColumn;

            var result = await FetchPageAsync(CurrentPage, PageSize, SearchText, sortParam);

            if (result.IsSuccess && result.Data is not null)
            {
                var page = result.Data;
                Items.Clear();
                foreach (var item in page.Items)
                    Items.Add(item);

                TotalCount = page.TotalCount;
                TotalPages = page.TotalPages;
                IsEmpty = Items.Count == 0;
                UpdatePagingInfo();

                await OnPageLoadedAsync();
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Derived classes implement this to call the specific API endpoint.
    /// </summary>
    protected abstract Task<ApiResult<PagedResponse<T>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort);

    /// <summary>
    /// Called after a page is successfully loaded. Override to perform post-load processing.
    /// </summary>
    protected virtual Task OnPageLoadedAsync() => Task.CompletedTask;

    /// <summary>
    /// Builds the standard query string for paged endpoints.
    /// </summary>
    protected static string BuildQueryString(string baseUrl, int page, int pageSize, string? search, string? sort)
    {
        var qs = $"{baseUrl}?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            qs += $"&q={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(sort))
            qs += $"&sort={Uri.EscapeDataString(sort)}";
        return qs;
    }

    private void UpdatePagingInfo()
    {
        var pageLbl = Localization.Strings.Paging_Page;
        var ofLbl = Localization.Strings.Paging_Of;
        var itemsLbl = Localization.Strings.Paging_Items;
        PagingInfo = $"{pageLbl} {CurrentPage} {ofLbl} {TotalPages} ({TotalCount} {itemsLbl})";
    }

    private async Task DelayOverlayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(OverlayDelayMs, ct);
            if (!ct.IsCancellationRequested && IsLoading)
                IsLoadingOverlayVisible = true;
        }
        catch (TaskCanceledException)
        {
        }
    }
}

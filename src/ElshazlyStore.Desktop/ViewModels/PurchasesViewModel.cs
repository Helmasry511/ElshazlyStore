using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Helpers;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class PurchasesViewModel : PagedListViewModelBase<PurchaseDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly INavigationService _navigationService;
    private readonly IStockChangeNotifier _stockNotifier;
    private CancellationTokenSource? _supplierSearchCts;
    private CancellationTokenSource? _variantSearchCts;

    public PurchasesViewModel(
        ApiClient apiClient,
        IPermissionService permissionService,
        IMessageService messageService,
        INavigationService navigationService,
        IStockChangeNotifier stockNotifier)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        _navigationService = navigationService;
        _stockNotifier = stockNotifier;
        Title = Localization.Strings.Nav_Purchases;
        CanWrite = _permissionService.HasPermission(PermissionCodes.PurchasesWrite);
        CanPost = _permissionService.HasPermission(PermissionCodes.PurchasesPost);
    }

    [ObservableProperty] private bool _canWrite;
    [ObservableProperty] private bool _canPost;

    // ── Detail modal ──
    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private PurchaseDto? _selectedPurchase;

    // ── Create/Edit modal ──
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _formDocumentNumber = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private bool _isSaving;
    private Guid? _editingId;

    // ── Supplier typeahead ──
    [ObservableProperty] private string _supplierSearchText = string.Empty;
    [ObservableProperty] private bool _hasSupplierSearchResults;
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    public ObservableCollection<SupplierDto> SupplierSearchResults { get; } = [];

    // ── Warehouse picker ──
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];
    [ObservableProperty] private WarehouseDto? _selectedWarehouse;

    // ── Variant typeahead for lines ──
    [ObservableProperty] private string _variantSearchText = string.Empty;
    [ObservableProperty] private bool _hasVariantSearchResults;
    [ObservableProperty] private bool _isVariantSearchOpen;
    [ObservableProperty] private bool _isVariantSearchLoading;
    [ObservableProperty] private string _variantSearchNote = string.Empty;
    public ObservableCollection<VariantListDto> VariantSearchResults { get; } = [];

    // ── Lines ──
    public ObservableCollection<PurchaseLineVm> Lines { get; } = [];
    private PurchaseLineVm? _editingLine;

    [ObservableProperty] private bool _isInitialized;

    // ── Notification ──
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";

    // ── Pre-set supplier filter (from Supplier details → purchases) ──
    private Guid? _presetSupplierId;
    private string? _presetSupplierName;

    public void SetSupplierFilter(Guid supplierId, string supplierName)
    {
        _presetSupplierId = supplierId;
        _presetSupplierName = supplierName;
    }

    protected override Task<ApiResult<PagedResponse<PurchaseDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/purchases", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<PurchaseDto>>(url);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized) return;
        await LoadWarehousesAsync();
        IsInitialized = true;
    }

    private async Task LoadWarehousesAsync()
    {
        var result = await ApiClient.GetAsync<PagedResponse<WarehouseDto>>(
            "/api/v1/warehouses?page=1&pageSize=500");
        Warehouses.Clear();
        if (result.IsSuccess && result.Data is not null)
        {
            foreach (var w in result.Data.Items.Where(w => w.IsActive))
                Warehouses.Add(w);
        }
    }

    // ═══ Detail Modal ═══

    [RelayCommand]
    private async Task OpenDetailAsync(PurchaseDto? purchase)
    {
        if (purchase is null) return;
        IsBusy = true;
        var result = await ApiClient.GetAsync<PurchaseDto>($"/api/v1/purchases/{purchase.Id}");
        IsBusy = false;
        if (result.IsSuccess && result.Data is not null)
        {
            SelectedPurchase = result.Data;
            IsDetailOpen = true;
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
        SelectedPurchase = null;
    }

    // ═══ Create/Edit Modal ═══

    [RelayCommand]
    private void OpenCreate()
    {
        _editingId = null;
        FormDocumentNumber = string.Empty;
        FormNotes = string.Empty;
        FormError = string.Empty;
        SelectedSupplier = null;
        SupplierSearchText = string.Empty;
        SelectedWarehouse = null;
        CloseVariantSearch();
        Lines.Clear();
        Lines.Add(new PurchaseLineVm());
        IsEditing = true;
    }

    [RelayCommand]
    private async Task OpenEditAsync(PurchaseDto? purchase)
    {
        if (purchase is null) return;
        // Can only edit drafts
        if (purchase.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.Purchase_CannotEditPosted);
            return;
        }

        IsBusy = true;
        var result = await ApiClient.GetAsync<PurchaseDto>($"/api/v1/purchases/{purchase.Id}");
        IsBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        var detail = result.Data;
        _editingId = detail.Id;
        FormDocumentNumber = detail.DocumentNumber ?? string.Empty;
        FormNotes = detail.Notes ?? string.Empty;
        FormError = string.Empty;

        // Match supplier
        SelectedSupplier = new SupplierDto { Id = detail.SupplierId, Name = detail.SupplierName };
        SupplierSearchText = detail.SupplierName;

        // Match warehouse
        SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == detail.WarehouseId);

        // Load lines
        Lines.Clear();
        if (detail.Lines is not null)
        {
            foreach (var line in detail.Lines)
            {
                var meta = BuildColorSizeMeta(line.Color, line.Size);
                var display = string.IsNullOrEmpty(meta)
                    ? $"{line.ProductName} — {line.VariantSku}"
                    : $"{line.ProductName} ({meta}) — {line.VariantSku}";

                Lines.Add(new PurchaseLineVm
                {
                    VariantId = line.VariantId,
                    VariantDisplay = display,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost
                });
            }
        }

        if (Lines.Count == 0)
            Lines.Add(new PurchaseLineVm());

        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        CloseVariantSearch();
        IsEditing = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        FormError = string.Empty;

        if (SelectedSupplier is null)
        {
            FormError = Localization.Strings.Purchase_SupplierRequired;
            return;
        }
        if (SelectedWarehouse is null)
        {
            FormError = Localization.Strings.Validation_WarehouseRequired;
            return;
        }
        if (!SelectedWarehouse.IsActive)
        {
            FormError = Localization.Strings.Validation_WarehouseInactive;
            return;
        }
        if (Lines.Count == 0 || Lines.All(l => l.VariantId == Guid.Empty))
        {
            FormError = Localization.Strings.Validation_LinesRequired;
            return;
        }

        var validLines = Lines.Where(l => l.VariantId != Guid.Empty).ToList();
        foreach (var line in validLines)
        {
            if (line.Quantity <= 0)
            {
                FormError = Localization.Strings.Validation_QuantityRequired;
                return;
            }
        }

        IsSaving = true;
        try
        {
            var lineRequests = validLines.Select(l => new PurchaseLineRequest
            {
                VariantId = l.VariantId,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost
            }).ToList();

            if (_editingId is null)
            {
                var body = new CreatePurchaseRequest
                {
                    SupplierId = SelectedSupplier.Id,
                    WarehouseId = SelectedWarehouse.Id,
                    DocumentNumber = string.IsNullOrWhiteSpace(FormDocumentNumber) ? null : FormDocumentNumber.Trim(),
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };
                var result = await ApiClient.PostAsync<PurchaseDto>("/api/v1/purchases", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
                NotificationMessage = Localization.Strings.Purchase_Created;
                NotificationType = "Success";
            }
            else
            {
                var body = new UpdatePurchaseRequest
                {
                    SupplierId = SelectedSupplier.Id,
                    WarehouseId = SelectedWarehouse.Id,
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };
                var result = await ApiClient.PutAsync<PurchaseDto>($"/api/v1/purchases/{_editingId}", body);
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

    // ═══ Post / Delete ═══

    [RelayCommand]
    private async Task PostPurchaseAsync(PurchaseDto? purchase)
    {
        if (purchase is null) return;
        if (purchase.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.Purchase_AlreadyPosted);
            return;
        }
        if (!_messageService.ShowConfirm(Localization.Strings.Purchase_ConfirmPost))
            return;

        IsBusy = true;
        var result = await ApiClient.PostAsync<object>($"/api/v1/purchases/{purchase.Id}/post");
        IsBusy = false;

        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        NotificationMessage = Localization.Strings.Purchase_PostSuccess;
        NotificationType = "Success";
        _stockNotifier.NotifyStockChanged();
        await LoadPageAsync();

        // If detail was open, refresh it
        if (IsDetailOpen && SelectedPurchase?.Id == purchase.Id)
            await OpenDetailAsync(purchase);
    }

    [RelayCommand]
    private async Task DeletePurchaseAsync(PurchaseDto? purchase)
    {
        if (purchase is null) return;
        if (purchase.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.Purchase_CannotDeletePosted);
            return;
        }
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDelete))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/purchases/{purchase.Id}");
        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }
        await LoadPageAsync();
    }

    // ═══ Supplier Typeahead ═══

    partial void OnSupplierSearchTextChanged(string value)
    {
        _supplierSearchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            SupplierSearchResults.Clear();
            HasSupplierSearchResults = false;
            return;
        }
        _supplierSearchCts = new CancellationTokenSource();
        _ = DebounceSearchSuppliersAsync(value.Trim(), _supplierSearchCts.Token);
    }

    private async Task DebounceSearchSuppliersAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;
            var url = $"/api/v1/suppliers?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
            var result = await ApiClient.GetAsync<PagedResponse<SupplierDto>>(url);
            if (ct.IsCancellationRequested) return;
            SupplierSearchResults.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var s in result.Data.Items)
                    SupplierSearchResults.Add(s);
            }
            HasSupplierSearchResults = SupplierSearchResults.Count > 0;
        }
        catch (TaskCanceledException) { }
    }

    [RelayCommand]
    private void SelectSupplier(SupplierDto? supplier)
    {
        if (supplier is null) return;
        SelectedSupplier = supplier;
        SupplierSearchText = supplier.Name;
        SupplierSearchResults.Clear();
        HasSupplierSearchResults = false;
    }

    // ═══ Variant Typeahead ═══

    partial void OnVariantSearchTextChanged(string value)
    {
        _variantSearchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            VariantSearchResults.Clear();
            HasVariantSearchResults = false;
            IsVariantSearchLoading = false;
            VariantSearchNote = value.Trim().Length == 0
                ? Localization.Strings.Variant_SearchMinChars
                : Localization.Strings.Variant_SearchMinChars;
            return;
        }
        VariantSearchNote = string.Empty;
        _variantSearchCts = new CancellationTokenSource();
        _ = DebounceSearchVariantsAsync(value.Trim(), _variantSearchCts.Token);
    }

    private async Task DebounceSearchVariantsAsync(string query, CancellationToken ct)
    {
        try
        {
            IsVariantSearchLoading = true;
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;
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
            if (!HasVariantSearchResults)
            {
                VariantSearchNote = result.IsSuccess
                    ? Localization.Strings.Variant_NoResults
                    : result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            }
            else
            {
                VariantSearchNote = string.Empty;
            }
        }
        catch (TaskCanceledException) { }
        finally
        {
            IsVariantSearchLoading = false;
        }
    }

    [RelayCommand]
    private void StartLineVariantSearch(PurchaseLineVm? line)
    {
        _editingLine = line;
        VariantSearchText = string.Empty;
        VariantSearchResults.Clear();
        HasVariantSearchResults = false;
        VariantSearchNote = Localization.Strings.Variant_SearchMinChars;
        IsVariantSearchOpen = true;
    }

    [RelayCommand]
    private void SelectVariantForLine(VariantListDto? variant)
    {
        if (variant is null || _editingLine is null) return;
        _editingLine.VariantId = variant.Id;
        var meta = BuildColorSizeMeta(variant.Color, variant.Size);
        _editingLine.VariantDisplay = string.IsNullOrEmpty(meta)
            ? $"{variant.ProductName} — {variant.Sku}"
            : $"{variant.ProductName} ({meta}) — {variant.Sku}";
        CloseVariantSearch();
    }

    private void CloseVariantSearch()
    {
        VariantSearchResults.Clear();
        HasVariantSearchResults = false;
        VariantSearchText = string.Empty;
        VariantSearchNote = string.Empty;
        IsVariantSearchOpen = false;
        IsVariantSearchLoading = false;
        _editingLine = null;
    }

    [RelayCommand]
    private void AddLine() => Lines.Add(new PurchaseLineVm());

    [RelayCommand]
    private void RemoveLine(PurchaseLineVm? line)
    {
        if (line is not null) Lines.Remove(line);
    }

    // ═══ Print ═══

    [RelayCommand]
    private async Task PrintPurchaseDocAsync(PurchaseDto? purchase)
    {
        if (purchase is null) return;
        IsBusy = true;
        var result = await ApiClient.GetAsync<PurchaseDto>($"/api/v1/purchases/{purchase.Id}");
        IsBusy = false;
        if (result.IsSuccess && result.Data is not null)
        {
            DocumentPrintHelper.PrintPurchase(result.Data);
        }
        else
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
        }
    }

    // ═══ Navigation helpers ═══

    [RelayCommand]
    private void NavigateToPurchaseReturns()
    {
        _navigationService.NavigateTo<PurchaseReturnsViewModel>();
    }

    private static string BuildColorSizeMeta(string? color, string? size)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!);
        if (!string.IsNullOrWhiteSpace(size)) parts.Add(size!);
        return string.Join(" / ", parts);
    }
}

public sealed partial class PurchaseLineVm : ObservableObject
{
    [ObservableProperty] private Guid _variantId;
    [ObservableProperty] private string _variantDisplay = string.Empty;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitCost;

    public decimal LineTotal => Quantity * UnitCost;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitCostChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

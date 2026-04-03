using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Helpers;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class PurchaseReturnsViewModel : PagedListViewModelBase<PurchaseReturnDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly INavigationService _navigationService;
    private readonly IStockChangeNotifier _stockNotifier;
    private CancellationTokenSource? _supplierSearchCts;
    private CancellationTokenSource? _variantSearchCts;

    public PurchaseReturnsViewModel(
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
        Title = Localization.Strings.Nav_PurchaseReturns;
        CanCreate = _permissionService.HasPermission(PermissionCodes.PurchaseReturnCreate);
        CanPost = _permissionService.HasPermission(PermissionCodes.PurchaseReturnPost);
        CanVoid = _permissionService.HasPermission(PermissionCodes.PurchaseReturnVoid);
    }

    [ObservableProperty] private bool _canCreate;
    [ObservableProperty] private bool _canPost;
    [ObservableProperty] private bool _canVoid;

    // ── Detail modal ──
    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private PurchaseReturnDto? _selectedReturn;

    // ── Create/Edit modal ──
    [ObservableProperty] private bool _isEditing;
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

    // ── Original purchase picker ──
    public ObservableCollection<PurchaseDto> PostedPurchases { get; } = [];
    [ObservableProperty] private PurchaseDto? _selectedOriginalPurchase;

    // ── Reason codes ──
    public ObservableCollection<ReasonCodeDto> ReasonCodes { get; } = [];
    [ObservableProperty] private bool _hasNoReasonCodes;

    // ── Inline add reason code ──
    [ObservableProperty] private bool _isAddingReasonCode;
    [ObservableProperty] private string _newReasonCode = string.Empty;
    [ObservableProperty] private string _newReasonName = string.Empty;
    [ObservableProperty] private string _newReasonNotes = string.Empty;
    [ObservableProperty] private string _newReasonError = string.Empty;
    [ObservableProperty] private bool _isSavingReason;

    // ── Variant typeahead for lines ──
    [ObservableProperty] private string _variantSearchText = string.Empty;
    [ObservableProperty] private bool _hasVariantSearchResults;
    [ObservableProperty] private bool _isVariantSearchOpen;
    [ObservableProperty] private bool _isVariantSearchLoading;
    [ObservableProperty] private string _variantSearchNote = string.Empty;
    public ObservableCollection<VariantListDto> VariantSearchResults { get; } = [];

    // ── Lines ──
    public ObservableCollection<PurchaseReturnLineVm> Lines { get; } = [];
    private PurchaseReturnLineVm? _editingLine;

    [ObservableProperty] private bool _isInitialized;

    // ── Notification ──
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";

    protected override Task<ApiResult<PagedResponse<PurchaseReturnDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/purchase-returns", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<PurchaseReturnDto>>(url);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized) return;
        await LoadWarehousesAsync();
        await LoadReasonCodesAsync();
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

    private async Task LoadReasonCodesAsync()
    {
        var result = await ApiClient.GetAsync<PagedResponse<ReasonCodeDto>>(
            "/api/v1/reasons?page=1&pageSize=100&isActive=true");
        ReasonCodes.Clear();
        if (result.IsSuccess && result.Data is not null)
        {
            foreach (var rc in result.Data.Items)
                ReasonCodes.Add(rc);
        }
        HasNoReasonCodes = ReasonCodes.Count == 0;
    }

    // ═══ Detail Modal ═══

    [RelayCommand]
    private async Task OpenDetailAsync(PurchaseReturnDto? ret)
    {
        if (ret is null) return;
        IsBusy = true;
        var result = await ApiClient.GetAsync<PurchaseReturnDto>($"/api/v1/purchase-returns/{ret.Id}");
        IsBusy = false;
        if (result.IsSuccess && result.Data is not null)
        {
            SelectedReturn = result.Data;
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
        SelectedReturn = null;
    }

    // ═══ Create/Edit Modal ═══

    [RelayCommand]
    private async Task OpenCreateAsync()
    {
        _editingId = null;
        FormNotes = string.Empty;
        FormError = string.Empty;
        SelectedSupplier = null;
        SupplierSearchText = string.Empty;
        SelectedWarehouse = null;
        SelectedOriginalPurchase = null;
        CloseVariantSearch();
        CloseInlineReasonAdd();
        Lines.Clear();
        Lines.Add(new PurchaseReturnLineVm());
        await LoadReasonCodesAsync();
        IsEditing = true;
    }

    [RelayCommand]
    private async Task OpenEditAsync(PurchaseReturnDto? ret)
    {
        if (ret is null) return;
        if (ret.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.PurchaseReturn_CannotEditPosted);
            return;
        }

        IsBusy = true;
        var result = await ApiClient.GetAsync<PurchaseReturnDto>($"/api/v1/purchase-returns/{ret.Id}");
        IsBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        var detail = result.Data;
        _editingId = detail.Id;
        FormNotes = detail.Notes ?? string.Empty;
        FormError = string.Empty;

        SelectedSupplier = new SupplierDto { Id = detail.SupplierId, Name = detail.SupplierName };
        SupplierSearchText = detail.SupplierName;
        SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == detail.WarehouseId);

        Lines.Clear();
        if (detail.Lines is not null)
        {
            foreach (var line in detail.Lines)
            {
                var meta = BuildColorSizeMeta(line.Color, line.Size);
                var display = string.IsNullOrEmpty(meta)
                    ? $"{line.ProductName} — {line.VariantSku}"
                    : $"{line.ProductName} ({meta}) — {line.VariantSku}";

                Lines.Add(new PurchaseReturnLineVm
                {
                    VariantId = line.VariantId,
                    VariantDisplay = display,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    SelectedReasonCode = ReasonCodes.FirstOrDefault(rc => rc.Id == line.ReasonCodeId),
                    DispositionType = ParseDispositionType(line.DispositionType),
                    LineNotes = line.Notes ?? string.Empty
                });
            }
        }

        if (Lines.Count == 0)
            Lines.Add(new PurchaseReturnLineVm());

        CloseInlineReasonAdd();
        await LoadReasonCodesAsync();

        // Re-match reason codes after refresh
        foreach (var line in Lines)
        {
            if (line.SelectedReasonCode is not null)
                line.SelectedReasonCode = ReasonCodes.FirstOrDefault(rc => rc.Id == line.SelectedReasonCode.Id);
        }

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

        var validLines = Lines.Where(l => l.VariantId != Guid.Empty).ToList();
        if (validLines.Count == 0)
        {
            FormError = Localization.Strings.Validation_LinesRequired;
            return;
        }

        foreach (var line in validLines)
        {
            if (line.Quantity <= 0)
            {
                FormError = Localization.Strings.Validation_QuantityRequired;
                return;
            }
            if (line.SelectedReasonCode is null)
            {
                FormError = Localization.Strings.PurchaseReturn_ReasonRequired;
                return;
            }
        }

        IsSaving = true;
        try
        {
            var lineRequests = validLines.Select(l => new PurchaseReturnLineRequest
            {
                VariantId = l.VariantId,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost,
                ReasonCodeId = l.SelectedReasonCode!.Id,
                DispositionType = l.DispositionType,
                Notes = string.IsNullOrWhiteSpace(l.LineNotes) ? null : l.LineNotes.Trim()
            }).ToList();

            if (_editingId is null)
            {
                var body = new CreatePurchaseReturnRequest
                {
                    SupplierId = SelectedSupplier.Id,
                    WarehouseId = SelectedWarehouse.Id,
                    OriginalPurchaseReceiptId = SelectedOriginalPurchase?.Id,
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };
                var result = await ApiClient.PostAsync<PurchaseReturnDto>("/api/v1/purchase-returns", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
                NotificationMessage = Localization.Strings.PurchaseReturn_Created;
                NotificationType = "Success";
            }
            else
            {
                var body = new UpdatePurchaseReturnRequest
                {
                    SupplierId = SelectedSupplier.Id,
                    WarehouseId = SelectedWarehouse.Id,
                    OriginalPurchaseReceiptId = SelectedOriginalPurchase?.Id,
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };
                var result = await ApiClient.PutAsync<PurchaseReturnDto>($"/api/v1/purchase-returns/{_editingId}", body);
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

    // ═══ Post / Void / Delete ═══

    [RelayCommand]
    private async Task PostReturnAsync(PurchaseReturnDto? ret)
    {
        if (ret is null) return;
        if (ret.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.PurchaseReturn_AlreadyPosted);
            return;
        }
        if (!_messageService.ShowConfirm(Localization.Strings.PurchaseReturn_ConfirmPost))
            return;

        IsBusy = true;
        var result = await ApiClient.PostAsync<object>($"/api/v1/purchase-returns/{ret.Id}/post");
        IsBusy = false;

        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        NotificationMessage = Localization.Strings.PurchaseReturn_PostSuccess;
        NotificationType = "Success";
        _stockNotifier.NotifyStockChanged();
        await LoadPageAsync();

        if (IsDetailOpen && SelectedReturn?.Id == ret.Id)
            await OpenDetailAsync(ret);
    }

    [RelayCommand]
    private async Task VoidReturnAsync(PurchaseReturnDto? ret)
    {
        if (ret is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.PurchaseReturn_ConfirmVoid))
            return;

        IsBusy = true;
        var result = await ApiClient.PostAsync<object>($"/api/v1/purchase-returns/{ret.Id}/void");
        IsBusy = false;

        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        NotificationMessage = Localization.Strings.PurchaseReturn_VoidSuccess;
        NotificationType = "Success";
        await LoadPageAsync();

        if (IsDetailOpen && SelectedReturn?.Id == ret.Id)
            await OpenDetailAsync(ret);
    }

    [RelayCommand]
    private async Task DeleteReturnAsync(PurchaseReturnDto? ret)
    {
        if (ret is null) return;
        if (ret.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.PurchaseReturn_CannotDeletePosted);
            return;
        }
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDelete))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/purchase-returns/{ret.Id}");
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
            VariantSearchNote = Localization.Strings.Variant_SearchMinChars;
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
    private void StartLineVariantSearch(PurchaseReturnLineVm? line)
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
    private void AddLine() => Lines.Add(new PurchaseReturnLineVm());

    [RelayCommand]
    private void RemoveLine(PurchaseReturnLineVm? line)
    {
        if (line is not null) Lines.Remove(line);
    }

    private static string BuildColorSizeMeta(string? color, string? size)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!);
        if (!string.IsNullOrWhiteSpace(size)) parts.Add(size!);
        return string.Join(" / ", parts);
    }

    // ═══ Print ═══

    [RelayCommand]
    private async Task PrintReturnDocAsync(PurchaseReturnDto? ret)
    {
        if (ret is null) return;
        IsBusy = true;
        var result = await ApiClient.GetAsync<PurchaseReturnDto>($"/api/v1/purchase-returns/{ret.Id}");
        IsBusy = false;
        if (result.IsSuccess && result.Data is not null)
        {
            DocumentPrintHelper.PrintPurchaseReturn(result.Data);
        }
        else
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
        }
    }

    private static int ParseDispositionType(string? s) => s switch
    {
        "Scrap" => 0,
        "Rework" => 1,
        "ReturnToVendor" => 2,
        "ReturnToStock" => 3,
        "Quarantine" => 4,
        "WriteOff" => 5,
        _ => 0
    };

    // ═══ Navigate to Reason Codes page ═══

    [RelayCommand]
    private void NavigateToReasonCodes()
    {
        IsEditing = false;
        _navigationService.NavigateTo<ReasonCodesViewModel>();
    }

    // ═══ Inline Add Reason Code ═══

    [RelayCommand]
    private void OpenInlineReasonAdd()
    {
        NewReasonCode = string.Empty;
        NewReasonName = string.Empty;
        NewReasonNotes = string.Empty;
        NewReasonError = string.Empty;
        IsAddingReasonCode = true;
    }

    [RelayCommand]
    private void CancelInlineReasonAdd() => CloseInlineReasonAdd();

    [RelayCommand]
    private async Task SaveInlineReasonAsync()
    {
        NewReasonError = string.Empty;

        if (string.IsNullOrWhiteSpace(NewReasonCode))
        {
            NewReasonError = Localization.Strings.ReasonCode_CodeRequired;
            return;
        }
        if (string.IsNullOrWhiteSpace(NewReasonName))
        {
            NewReasonError = Localization.Strings.ReasonCode_NameRequired;
            return;
        }

        IsSavingReason = true;
        try
        {
            var body = new
            {
                code = NewReasonCode.Trim().ToUpperInvariant(),
                nameAr = NewReasonName.Trim(),
                description = string.IsNullOrWhiteSpace(NewReasonNotes) ? (string?)null : NewReasonNotes.Trim(),
                category = "PurchaseReturn",
                requiresManagerApproval = false
            };

            var result = await ApiClient.PostAsync<ReasonCodeDto>("/api/v1/reasons", body);
            if (!result.IsSuccess)
            {
                NewReasonError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                return;
            }

            CloseInlineReasonAdd();
            await LoadReasonCodesAsync();

            // Auto-select the new reason if returned
            if (result.Data is not null)
            {
                var freshCode = ReasonCodes.FirstOrDefault(rc => rc.Id == result.Data.Id);
                if (freshCode is not null)
                {
                    foreach (var line in Lines.Where(l => l.SelectedReasonCode is null))
                    {
                        line.SelectedReasonCode = freshCode;
                        break; // select for first empty line only
                    }
                }
            }
        }
        finally
        {
            IsSavingReason = false;
        }
    }

    private void CloseInlineReasonAdd()
    {
        IsAddingReasonCode = false;
        NewReasonCode = string.Empty;
        NewReasonName = string.Empty;
        NewReasonNotes = string.Empty;
        NewReasonError = string.Empty;
    }
}

public sealed partial class PurchaseReturnLineVm : ObservableObject
{
    [ObservableProperty] private Guid _variantId;
    [ObservableProperty] private string _variantDisplay = string.Empty;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitCost;
    [ObservableProperty] private ReasonCodeDto? _selectedReasonCode;
    [ObservableProperty] private int _dispositionType;
    [ObservableProperty] private string _lineNotes = string.Empty;

    public decimal LineTotal => Quantity * UnitCost;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitCostChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

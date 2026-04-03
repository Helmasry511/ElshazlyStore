using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Helpers;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class SalesReturnsViewModel : PagedListViewModelBase<SalesReturnDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly IStockChangeNotifier _stockNotifier;
    private CancellationTokenSource? _invoiceSearchCts;

    // Disposition integer values per the enum in the backend:
    // Scrap=0, Rework=1, ReturnToVendor=2, ReturnToStock=3, Quarantine=4, WriteOff=5
    private const int DispositionReturnToStock = 3;
    private const int DispositionQuarantine = 4;

    private Guid? _editingId;

    public SalesReturnsViewModel(
        ApiClient apiClient,
        IPermissionService permissionService,
        IMessageService messageService,
        IStockChangeNotifier stockNotifier)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        _stockNotifier = stockNotifier;

        Title = Localization.Strings.Nav_SalesReturns;
        CanCreate = _permissionService.HasPermission(PermissionCodes.SalesReturnCreate);
        CanPost = _permissionService.HasPermission(PermissionCodes.SalesReturnPost);
    }

    // ── Permissions ──
    [ObservableProperty] private bool _canCreate;
    [ObservableProperty] private bool _canPost;

    // ── Notification ──
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";

    // ── Initialization ──
    [ObservableProperty] private bool _isInitialized;

    // ── Detail modal ──
    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private SalesReturnDto? _selectedReturn;

    // ── Create / Edit modal ──
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _formNotes = string.Empty;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private bool _isSaving;

    // ── Warehouse picker ──
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];
    [ObservableProperty] private WarehouseDto? _selectedWarehouse;

    // ── Invoice typeahead for "original sales invoice" ──
    [ObservableProperty] private string _invoiceSearchText = string.Empty;
    [ObservableProperty] private bool _hasInvoiceSearchResults;
    [ObservableProperty] private bool _isInvoiceSearchLoading;
    [ObservableProperty] private string _invoiceSearchNote = string.Empty;
    [ObservableProperty] private SaleDto? _selectedOriginalInvoice;
    public bool HasInvoiceSelected => SelectedOriginalInvoice is not null;
    public ObservableCollection<SaleDto> InvoiceSearchResults { get; } = [];

    // ── Invoice details loaded from selection ──
    [ObservableProperty] private bool _isLoadingInvoice;
    [ObservableProperty] private string _invoiceInfoError = string.Empty;

    // ── Customer derived from invoice (read-only in edit mode) ──
    [ObservableProperty] private string _formCustomerName = string.Empty;
    private Guid? _invoiceCustomerId;

    // ── Reason codes ──
    public ObservableCollection<ReasonCodeDto> ReasonCodes { get; } = [];
    [ObservableProperty] private bool _hasNoReasonCodes;

    // ── Lines grid ──
    public ObservableCollection<SalesReturnLineVm> Lines { get; } = [];

    // ── Supported dispositions (SR-1: ReturnToStock + Quarantine only) ──
    public static IReadOnlyList<DispositionOption> SupportedDispositions { get; } =
    [
        new(DispositionReturnToStock, Localization.Strings.SalesReturn_DispositionReturnToStock),
        new(DispositionQuarantine,   Localization.Strings.SalesReturn_DispositionQuarantine),
    ];

    protected override Task<ApiResult<PagedResponse<SalesReturnDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/sales-returns", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<SalesReturnDto>>(url);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Initialization
    // ──────────────────────────────────────────────────────────────────────────

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

    // ──────────────────────────────────────────────────────────────────────────
    // Detail modal
    // ──────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenDetailAsync(SalesReturnDto? ret)
    {
        if (ret is null) return;
        IsBusy = true;
        var result = await ApiClient.GetAsync<SalesReturnDto>($"/api/v1/sales-returns/{ret.Id}");
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

    // ──────────────────────────────────────────────────────────────────────────
    // Create modal
    // ──────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenCreateAsync()
    {
        _editingId = null;
        IsEditMode = false;
        ResetForm();
        await LoadReasonCodesAsync();
        IsEditing = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Edit modal (draft only)
    // ──────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenEditAsync(SalesReturnDto? ret)
    {
        if (ret is null) return;
        if (ret.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.SalesReturn_CannotEditPosted);
            return;
        }

        IsBusy = true;
        var result = await ApiClient.GetAsync<SalesReturnDto>($"/api/v1/sales-returns/{ret.Id}");
        IsBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        await LoadReasonCodesAsync();

        var detail = result.Data;
        _editingId = detail.Id;
        IsEditMode = true;
        FormNotes = detail.Notes ?? string.Empty;
        FormError = string.Empty;
        InvoiceSearchText = string.Empty;
        InvoiceInfoError = string.Empty;
        FormCustomerName = detail.CustomerNameDisplay;
        _invoiceCustomerId = detail.CustomerId;

        SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == detail.WarehouseId);

        // Restore invoice reference display
        if (detail.OriginalSalesInvoiceId.HasValue && !string.IsNullOrEmpty(detail.OriginalInvoiceNumber))
        {
            SelectedOriginalInvoice = new SaleDto
            {
                Id = detail.OriginalSalesInvoiceId.Value,
                InvoiceNumber = detail.OriginalInvoiceNumber
            };
            InvoiceSearchText = detail.OriginalInvoiceNumber;
        }
        else
        {
            SelectedOriginalInvoice = null;
        }
        InvoiceSearchResults.Clear();
        HasInvoiceSearchResults = false;

        Lines.Clear();
        if (detail.Lines is not null)
        {
            foreach (var line in detail.Lines)
            {
                var meta = BuildColorSizeMeta(line.Color, line.Size);
                var display = string.IsNullOrEmpty(meta)
                    ? $"{line.ProductName} — {line.VariantSku}"
                    : $"{line.ProductName} ({meta}) — {line.VariantSku}";

                var dispositionInt = ParseDispositionInt(line.DispositionType);
                Lines.Add(new SalesReturnLineVm
                {
                    VariantId = line.VariantId,
                    VariantDisplay = display,
                    Sku = line.VariantSku,
                    SoldQty = line.Quantity,             // best available; original sold qty may differ
                    AvailableQty = line.Quantity,         // will be updated if invoice is re-loaded
                    ReturnQty = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    SelectedReasonCode = ReasonCodes.FirstOrDefault(rc => rc.Id == line.ReasonCodeId),
                    DispositionType = dispositionInt,
                    LineNotes = line.Notes ?? string.Empty
                });
            }
        }

        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetForm();
        IsEditing = false;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Invoice typeahead
    // ──────────────────────────────────────────────────────────────────────────

    partial void OnInvoiceSearchTextChanged(string value)
    {
        // In edit mode the invoice is locked; do not trigger search or clear the existing selection.
        if (IsEditMode) return;

        _invoiceSearchCts?.Cancel();

        if (SelectedOriginalInvoice is not null &&
            !string.Equals(value, SelectedOriginalInvoice.InvoiceNumber, StringComparison.Ordinal))
        {
            SelectedOriginalInvoice = null;
            Lines.Clear();
            InvoiceInfoError = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            InvoiceSearchResults.Clear();
            HasInvoiceSearchResults = false;
            IsInvoiceSearchLoading = false;
            InvoiceSearchNote = Localization.Strings.SalesReturn_InvoiceSearchHint;
            return;
        }

        InvoiceSearchNote = string.Empty;
        _invoiceSearchCts = new CancellationTokenSource();
        _ = DebounceSearchInvoicesAsync(value.Trim(), _invoiceSearchCts.Token);
    }

    partial void OnSelectedOriginalInvoiceChanged(SaleDto? value)
        => OnPropertyChanged(nameof(HasInvoiceSelected));

    private async Task DebounceSearchInvoicesAsync(string query, CancellationToken ct)
    {
        try
        {
            IsInvoiceSearchLoading = true;
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;

            var url = $"/api/v1/sales?page=1&pageSize=10&q={Uri.EscapeDataString(query)}";
            var result = await ApiClient.GetAsync<PagedResponse<SaleDto>>(url);
            if (ct.IsCancellationRequested) return;

            InvoiceSearchResults.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var inv in result.Data.Items.Where(i => i.Status == "Posted"))
                    InvoiceSearchResults.Add(inv);
            }

            HasInvoiceSearchResults = InvoiceSearchResults.Count > 0;
            InvoiceSearchNote = (result.IsSuccess && !HasInvoiceSearchResults)
                ? Localization.Strings.SalesReturn_NoInvoiceFound
                : (result.IsSuccess ? string.Empty : result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
        }
        catch (TaskCanceledException) { }
        finally
        {
            IsInvoiceSearchLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectInvoiceAsync(SaleDto? invoice)
    {
        if (invoice is null) return;
        SelectedOriginalInvoice = invoice;
        InvoiceSearchText = invoice.InvoiceNumber;
        InvoiceSearchResults.Clear();
        HasInvoiceSearchResults = false;
        InvoiceSearchNote = string.Empty;

        await LoadInvoiceDetailsAsync(invoice.Id);
    }

    private async Task LoadInvoiceDetailsAsync(Guid invoiceId)
    {
        InvoiceInfoError = string.Empty;
        IsLoadingInvoice = true;
        Lines.Clear();

        try
        {
            var result = await ApiClient.GetAsync<SaleDto>($"/api/v1/sales/{invoiceId}");
            if (!result.IsSuccess || result.Data is null)
            {
                InvoiceInfoError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                return;
            }

            var invoice = result.Data;

            // Validate invoice date — 15-day rule
            var invoiceDate = invoice.InvoiceDateUtc.ToLocalTime().Date;
            var today = DateTime.Today;
            if ((today - invoiceDate).TotalDays > 15)
            {
                InvoiceInfoError = Localization.Strings.SalesReturn_InvoiceTooOld;
                SelectedOriginalInvoice = null;
                InvoiceSearchText = string.Empty;
                return;
            }

            // Auto-populate warehouse and customer
            if (SelectedWarehouse is null)
                SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == invoice.WarehouseId);
            FormCustomerName = invoice.CustomerNameDisplay;
            _invoiceCustomerId = invoice.CustomerId;

            // Load lines from invoice; set initial available qty = sold qty
            Lines.Clear();
            if (invoice.Lines is not null)
            {
                foreach (var line in invoice.Lines)
                {
                    var meta = BuildColorSizeMeta(null, null);
                    var display = $"{line.ProductName} — {line.Sku}";

                    Lines.Add(new SalesReturnLineVm
                    {
                        VariantId = line.VariantId,
                        VariantDisplay = display,
                        Sku = line.Sku,
                        SoldQty = line.Quantity,
                        AvailableQty = line.Quantity,   // backend will validate; we show sold as ceiling
                        ReturnQty = 0,
                        UnitPrice = line.UnitPrice,
                        SelectedReasonCode = null,
                        DispositionType = DispositionReturnToStock,
                        LineNotes = string.Empty
                    });
                }
            }
        }
        finally
        {
            IsLoadingInvoice = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Save (Draft only)
    // ──────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        FormError = string.Empty;

        // SR-1 validation
        if (SelectedOriginalInvoice is null)
        {
            FormError = Localization.Strings.SalesReturn_InvoiceRequired;
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

        var validLines = Lines.Where(l => l.VariantId != Guid.Empty && l.ReturnQty > 0).ToList();
        if (validLines.Count == 0)
        {
            FormError = Localization.Strings.Validation_LinesRequired;
            return;
        }

        foreach (var line in validLines)
        {
            if (line.ReturnQty <= 0)
            {
                FormError = Localization.Strings.Validation_QuantityRequired;
                return;
            }
            if (line.ReturnQty > line.AvailableQty)
            {
                FormError = Localization.Strings.SalesReturn_QuantityExceedsAvailable;
                return;
            }
            if (line.SelectedReasonCode is null)
            {
                FormError = Localization.Strings.PurchaseReturn_ReasonRequired;
                return;
            }
            if (line.DispositionType != DispositionReturnToStock && line.DispositionType != DispositionQuarantine)
            {
                FormError = Localization.Strings.SalesReturn_DispositionNotAllowed;
                return;
            }
        }

        IsSaving = true;
        try
        {
            var lineRequests = validLines.Select(l => new SalesReturnLineRequest
            {
                VariantId = l.VariantId,
                Quantity = l.ReturnQty,
                UnitPrice = l.UnitPrice,
                ReasonCodeId = l.SelectedReasonCode!.Id,
                DispositionType = l.DispositionType,
                Notes = string.IsNullOrWhiteSpace(l.LineNotes) ? null : l.LineNotes.Trim()
            }).ToList();

            if (_editingId is null)
            {
                var body = new CreateSalesReturnRequest
                {
                    WarehouseId = SelectedWarehouse.Id,
                    CustomerId = _invoiceCustomerId,
                    OriginalSalesInvoiceId = SelectedOriginalInvoice.Id,
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };
                var result = await ApiClient.PostAsync<SalesReturnDto>("/api/v1/sales-returns", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
                NotificationMessage = Localization.Strings.SalesReturn_Created;
                NotificationType = "Success";
            }
            else
            {
                var body = new UpdateSalesReturnRequest
                {
                    WarehouseId = SelectedWarehouse.Id,
                    CustomerId = _invoiceCustomerId,
                    OriginalSalesInvoiceId = SelectedOriginalInvoice.Id,
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };
                var result = await ApiClient.PutAsync<SalesReturnDto>($"/api/v1/sales-returns/{_editingId}", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
                NotificationMessage = Localization.Strings.SalesReturn_Updated;
                NotificationType = "Success";
            }

            IsEditing = false;
            await LoadPageAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Delete (Draft only)
    // ──────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteReturnAsync(SalesReturnDto? ret)
    {
        if (ret is null) return;
        if (ret.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.SalesReturn_CannotDeletePosted);
            return;
        }
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDelete))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/sales-returns/{ret.Id}");
        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        NotificationMessage = Localization.Strings.SalesReturn_DeleteSuccess;
        NotificationType = "Success";
        await LoadPageAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Post (Draft → Posted)
    // ──────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task PostReturnAsync(SalesReturnDto? ret)
    {
        if (ret is null) return;

        if (ret.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.SalesReturn_CannotPostNotDraft);
            return;
        }

        // Fetch full detail so the confirmation dialog can list items and quantities.
        IsBusy = true;
        var detailResult = await ApiClient.GetAsync<SalesReturnDto>($"/api/v1/sales-returns/{ret.Id}");
        IsBusy = false;

        if (!detailResult.IsSuccess || detailResult.Data is null)
        {
            _messageService.ShowError(detailResult.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        var detail = detailResult.Data;

        // Build a human-readable confirmation message.
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Localization.Strings.SalesReturn_ConfirmPostHeader);
        sb.AppendLine();
        sb.AppendLine($"{Localization.Strings.SalesReturn_ReturnNumber}: {detail.DocumentNumber ?? "—"}");
        sb.AppendLine($"{Localization.Strings.SalesReturn_OriginalInvoice}: {detail.OriginalInvoiceNumber ?? "—"}");
        sb.AppendLine($"{Localization.Strings.Sales_Customer}: {detail.CustomerNameDisplay}");

        if (detail.Lines is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"{Localization.Strings.Purchase_Lines}:");
            foreach (var line in detail.Lines)
            {
                var name = string.IsNullOrWhiteSpace(line.ProductName) ? line.VariantSku : line.ProductName;
                sb.AppendLine($"  • {name}  ×  {line.Quantity:N0}");
            }
        }

        if (!_messageService.ShowConfirm(sb.ToString(), Localization.Strings.SalesReturn_ConfirmPostTitle))
            return;

        IsBusy = true;
        var result = await ApiClient.PostAsync<object>($"/api/v1/sales-returns/{ret.Id}/post");
        IsBusy = false;

        if (!result.IsSuccess)
        {
            NotificationMessage = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            NotificationType = "Error";
            return;
        }

        NotificationMessage = Localization.Strings.SalesReturn_PostSuccess;
        NotificationType = "Success";
        _stockNotifier.NotifyStockChanged();
        await LoadPageAsync();

        if (IsDetailOpen && SelectedReturn?.Id == ret.Id)
            await OpenDetailAsync(ret);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Print Sales Return document
    // ──────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task PrintSalesReturnAsync(SalesReturnDto? ret)
    {
        if (ret is null) return;
        IsBusy = true;
        var result = await ApiClient.GetAsync<SalesReturnDto>($"/api/v1/sales-returns/{ret.Id}");
        IsBusy = false;
        if (result.IsSuccess && result.Data is not null)
        {
            Helpers.DocumentPrintHelper.PrintSalesReturn(result.Data);
        }
        else
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void ResetForm()
    {
        FormNotes = string.Empty;
        FormError = string.Empty;
        FormCustomerName = string.Empty;
        _invoiceCustomerId = null;
        InvoiceSearchText = string.Empty;
        InvoiceSearchNote = string.Empty;
        InvoiceInfoError = string.Empty;
        SelectedOriginalInvoice = null;
        SelectedWarehouse = null;
        Lines.Clear();
        InvoiceSearchResults.Clear();
        HasInvoiceSearchResults = false;
        IsInvoiceSearchLoading = false;
    }

    private static string BuildColorSizeMeta(string? color, string? size)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!);
        if (!string.IsNullOrWhiteSpace(size)) parts.Add(size!);
        return string.Join(" / ", parts);
    }

    private static int ParseDispositionInt(string? s) => s switch
    {
        "Scrap" => 0,
        "Rework" => 1,
        "ReturnToVendor" => 2,
        "ReturnToStock" => 3,
        "Quarantine" => 4,
        "WriteOff" => 5,
        _ => DispositionReturnToStock
    };
}

// ──────────────────────────────────────────────────────────────────────────────
// Supporting types
// ──────────────────────────────────────────────────────────────────────────────

public sealed class SalesReturnLineVm : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private decimal _returnQty;
    private decimal _unitPrice;
    private ReasonCodeDto? _selectedReasonCode;
    private int _dispositionType;
    private string _lineNotes = string.Empty;

    public Guid VariantId { get; set; }
    public string VariantDisplay { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal SoldQty { get; set; }
    public decimal AvailableQty { get; set; }

    public decimal ReturnQty
    {
        get => _returnQty;
        set
        {
            if (SetProperty(ref _returnQty, value))
                OnPropertyChanged(nameof(LineTotal));
        }
    }

    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (SetProperty(ref _unitPrice, value))
                OnPropertyChanged(nameof(LineTotal));
        }
    }

    public decimal LineTotal => ReturnQty * UnitPrice;

    public ReasonCodeDto? SelectedReasonCode
    {
        get => _selectedReasonCode;
        set => SetProperty(ref _selectedReasonCode, value);
    }

    public int DispositionType
    {
        get => _dispositionType;
        set => SetProperty(ref _dispositionType, value);
    }

    public string LineNotes
    {
        get => _lineNotes;
        set => SetProperty(ref _lineNotes, value);
    }
}

public sealed class DispositionOption
{
    public DispositionOption(int value, string display)
    {
        Value = value;
        Display = display;
    }

    public int Value { get; }
    public string Display { get; }
}

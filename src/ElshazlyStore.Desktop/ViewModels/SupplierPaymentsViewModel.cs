using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Helpers;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class SupplierPaymentsViewModel : PagedListViewModelBase<PaymentDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private CancellationTokenSource? _supplierSearchCts;

    public SupplierPaymentsViewModel(
        ApiClient apiClient,
        IPermissionService permissionService,
        IMessageService messageService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        Title = Localization.Strings.Nav_SupplierPayments;
        CanWrite = _permissionService.HasPermission(PermissionCodes.PaymentsWrite);
    }

    [ObservableProperty] private bool _canWrite;

    // ── Create modal ──
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private bool _isSaving;

    // ── Supplier typeahead ──
    [ObservableProperty] private string _supplierSearchText = string.Empty;
    [ObservableProperty] private bool _hasSupplierSearchResults;
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    public ObservableCollection<SupplierDto> SupplierSearchResults { get; } = [];

    // ── Form fields ──
    [ObservableProperty] private decimal _formAmount;
    [ObservableProperty] private string _formMethod = "Cash";
    [ObservableProperty] private string _formWalletName = string.Empty;
    [ObservableProperty] private string _formReference = string.Empty;

    // ── Notification ──
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";

    [ObservableProperty] private bool _isInitialized;

    // Pre-set supplier filter
    private Guid? _presetSupplierId;
    private string? _presetSupplierName;

    public void SetSupplierFilter(Guid supplierId, string supplierName)
    {
        _presetSupplierId = supplierId;
        _presetSupplierName = supplierName;
    }

    public static string[] PaymentMethods => ["Cash", "Visa", "InstaPay"];

    protected override Task<ApiResult<PagedResponse<PaymentDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = $"/api/v1/payments?partyType=Supplier&page={page}&pageSize={pageSize}";
        if (_presetSupplierId.HasValue)
            url += $"&partyId={_presetSupplierId.Value}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&q={Uri.EscapeDataString(search)}";
        return ApiClient.GetAsync<PagedResponse<PaymentDto>>(url);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized) return;
        IsInitialized = true;
        if (_presetSupplierId.HasValue && _presetSupplierName is not null)
        {
            SelectedSupplier = new SupplierDto { Id = _presetSupplierId.Value, Name = _presetSupplierName };
            SupplierSearchText = _presetSupplierName;
        }
        await LoadPageAsync();
    }

    [RelayCommand]
    private void OpenCreate()
    {
        FormError = string.Empty;
        FormAmount = 0m;
        FormMethod = "Cash";
        FormWalletName = string.Empty;
        FormReference = string.Empty;

        if (_presetSupplierId.HasValue && _presetSupplierName is not null)
        {
            SelectedSupplier = new SupplierDto { Id = _presetSupplierId.Value, Name = _presetSupplierName };
            SupplierSearchText = _presetSupplierName;
        }
        else if (SelectedSupplier is null)
        {
            SupplierSearchText = string.Empty;
        }

        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
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
        if (FormAmount <= 0)
        {
            FormError = Localization.Strings.Payment_AmountRequired;
            return;
        }

        IsSaving = true;
        try
        {
            var body = new CreatePaymentRequest
            {
                PartyType = "Supplier",
                PartyId = SelectedSupplier.Id,
                Amount = FormAmount,
                Method = FormMethod,
                WalletName = string.IsNullOrWhiteSpace(FormWalletName) ? null : FormWalletName.Trim(),
                Reference = string.IsNullOrWhiteSpace(FormReference) ? null : FormReference.Trim(),
                PaymentDateUtc = DateTime.UtcNow
            };

            var result = await ApiClient.PostAsync<PaymentDto>("/api/v1/payments", body);
            if (!result.IsSuccess)
            {
                FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                return;
            }

            IsEditing = false;
            NotificationMessage = Localization.Strings.Payment_Created;
            NotificationType = "Success";
            await LoadPageAsync();
        }
        finally
        {
            IsSaving = false;
        }
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

        // Reload with supplier filter
        _presetSupplierId = supplier.Id;
        _presetSupplierName = supplier.Name;
        CurrentPage = 1;
        _ = LoadPageAsync();
    }

    // ═══ Print ═══

    [RelayCommand]
    private void PrintPayment(PaymentDto? payment)
    {
        if (payment is null) return;
        DocumentPrintHelper.PrintPaymentReceipt(payment);
    }
}

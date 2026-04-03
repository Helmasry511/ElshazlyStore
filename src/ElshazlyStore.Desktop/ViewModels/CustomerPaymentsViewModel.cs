using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Helpers;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class CustomerPaymentsViewModel : PagedListViewModelBase<PaymentDto>
{
    private readonly IPermissionService _permissionService;
    private readonly INavigationService _navigationService;
    private CancellationTokenSource? _customerSearchCts;
    private CancellationTokenSource? _modalCustomerSearchCts;
    /// <summary>Prevents toolbar CustomerSearchText changes from triggering the typeahead search.</summary>
    private bool _suppressCustomerSearch;
    /// <summary>Prevents modal ModalCustomerSearchText changes from triggering the typeahead search.</summary>
    private bool _suppressModalSearch;

    public CustomerPaymentsViewModel(
        ApiClient apiClient,
        IPermissionService permissionService,
        INavigationService navigationService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _navigationService = navigationService;
        Title = Localization.Strings.Nav_CustomerPayments;
        CanWrite = _permissionService.HasPermission(PermissionCodes.PaymentsWrite);
        CanViewAccounting = _permissionService.HasPermission(PermissionCodes.AccountingRead);
    }

    [ObservableProperty] private bool _canWrite;

    // ── Permissions ──
    [ObservableProperty] private bool _canViewAccounting;

    // ── Create modal ──
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private bool _isSaving;

    // ── Toolbar customer typeahead (page-level customer selection) ──
    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private bool _hasCustomerSearchResults;
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    public ObservableCollection<CustomerDto> CustomerSearchResults { get; } = [];

    // ── Modal customer typeahead (only active when page is in global/all-customers mode) ──
    [ObservableProperty] private string _modalCustomerSearchText = string.Empty;
    [ObservableProperty] private bool _hasModalCustomerSearchResults;
    public ObservableCollection<CustomerDto> ModalCustomerSearchResults { get; } = [];

    // ── Form fields ──
    [ObservableProperty] private decimal _formAmount;
    [ObservableProperty] private string _formMethod = "Cash";
    [ObservableProperty] private string _formWalletName = string.Empty;
    [ObservableProperty] private string _formReference = string.Empty;

    // ── Notification ──
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";

    [ObservableProperty] private bool _isInitialized;

    // ── Customer context (filter + identity) ──
    [ObservableProperty] private string _presetCustomerName = string.Empty;
    [ObservableProperty] private bool _hasPresetCustomer;

    // ── Outstanding display ──
    [ObservableProperty] private decimal? _customerOutstanding;
    [ObservableProperty] private bool _isLoadingOutstanding;

    /// <summary>
    /// When true, the create-modal customer field is shown as a readonly label (prefilled from page context).
    /// When false, the modal shows its own independent customer typeahead.
    /// </summary>
    public bool IsModalCustomerReadonly => HasPresetCustomer;

    // Outstanding display helpers
    public bool HasOutstandingDisplay => HasPresetCustomer && CanViewAccounting && CustomerOutstanding.HasValue && !IsLoadingOutstanding;
    public string OutstandingAmountDisplay => CustomerOutstanding.HasValue
        ? $"{CustomerOutstanding.Value:N2} جنيه"
        : string.Empty;

    // Pre-set customer filter (set by SetCustomerFilter before initialization)
    private Guid? _presetCustomerId;

    /// <summary>
    /// Called by navigation code before the page loads to pre-filter by a specific customer.
    /// Resets initialization so the page always loads fresh data for the new context.
    /// </summary>
    public void SetCustomerFilter(Guid customerId, string customerName)
    {
        _presetCustomerId = customerId;
        PresetCustomerName = customerName;
        HasPresetCustomer = true;
        CustomerOutstanding = null;
        IsInitialized = false;
    }

    // ── Property change notifications for computed states ──

    partial void OnHasPresetCustomerChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOutstandingDisplay));
        OnPropertyChanged(nameof(IsModalCustomerReadonly));
    }

    partial void OnCustomerOutstandingChanged(decimal? value)
    {
        OnPropertyChanged(nameof(HasOutstandingDisplay));
        OnPropertyChanged(nameof(OutstandingAmountDisplay));
    }

    partial void OnIsLoadingOutstandingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOutstandingDisplay));
    }

    partial void OnCanViewAccountingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOutstandingDisplay));
    }

    public static string[] PaymentMethods => ["Cash", "Visa", "InstaPay"];

    protected override Task<ApiResult<PagedResponse<PaymentDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = $"/api/v1/payments?partyType=Customer&page={page}&pageSize={pageSize}";
        if (_presetCustomerId.HasValue)
            url += $"&partyId={_presetCustomerId.Value}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&q={Uri.EscapeDataString(search)}";
        return ApiClient.GetAsync<PagedResponse<PaymentDto>>(url);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized) return;
        IsInitialized = true;
        if (_presetCustomerId.HasValue && !string.IsNullOrWhiteSpace(PresetCustomerName))
        {
            SelectedCustomer = new CustomerDto { Id = _presetCustomerId.Value, Name = PresetCustomerName };
            // Populate toolbar without triggering typeahead search
            _suppressCustomerSearch = true;
            CustomerSearchText = PresetCustomerName;
            _suppressCustomerSearch = false;
        }
        await LoadPageAsync();
        if (_presetCustomerId.HasValue && CanViewAccounting)
            await FetchOutstandingAsync(_presetCustomerId.Value);
    }

    [RelayCommand]
    private void OpenCreate()
    {
        FormError = string.Empty;
        FormAmount = 0m;
        FormMethod = "Cash";
        FormWalletName = string.Empty;
        FormReference = string.Empty;

        // Reset modal-specific customer search state without triggering modal search
        ModalCustomerSearchResults.Clear();
        HasModalCustomerSearchResults = false;
        _suppressModalSearch = true;
        ModalCustomerSearchText = string.Empty;
        _suppressModalSearch = false;

        if (_presetCustomerId.HasValue && !string.IsNullOrWhiteSpace(PresetCustomerName))
        {
            // Page is filtered to a specific customer — prefill and lock the modal customer field
            SelectedCustomer = new CustomerDto { Id = _presetCustomerId.Value, Name = PresetCustomerName };
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

        if (SelectedCustomer is null)
        {
            FormError = Localization.Strings.CustomerPayment_CustomerRequired;
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
                PartyType = "Customer",
                PartyId = SelectedCustomer.Id,
                Amount = FormAmount,
                Method = FormMethod,
                WalletName = string.IsNullOrWhiteSpace(FormWalletName) ? null : FormWalletName.Trim(),
                Reference = string.IsNullOrWhiteSpace(FormReference) ? null : FormReference.Trim(),
                PaymentDateUtc = DateTime.UtcNow
            };

            var result = await ApiClient.PostAsync<PaymentDto>("/api/v1/payments", body);
            if (!result.IsSuccess)
            {
                var baseMsg = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                // Augment the overpayment error with the known outstanding amount when available
                if (result.StatusCode == 422 && CustomerOutstanding.HasValue)
                    FormError = $"{baseMsg} ({CustomerOutstanding.Value:N2} جنيه)";
                else
                    FormError = baseMsg;
                return;
            }

            IsEditing = false;
            NotificationMessage = Localization.Strings.CustomerPayment_Created;
            NotificationType = "Success";
            await LoadPageAsync();
            // Refresh outstanding after a successful payment (balance decreases)
            if (_presetCustomerId.HasValue && CanViewAccounting)
                await FetchOutstandingAsync(_presetCustomerId.Value);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ═══ Toolbar Customer Typeahead ═══

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (_suppressCustomerSearch) return;
        _customerSearchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value))
        {
            CustomerSearchResults.Clear();
            HasCustomerSearchResults = false;
            // User explicitly cleared the toolbar search field → return to global/all-customers mode
            if (HasPresetCustomer)
            {
                _presetCustomerId = null;
                PresetCustomerName = string.Empty;
                HasPresetCustomer = false;
                SelectedCustomer = null;
                CustomerOutstanding = null;
                CurrentPage = 1;
                _ = LoadPageAsync();
            }
            return;
        }
        if (value.Trim().Length < 2)
        {
            CustomerSearchResults.Clear();
            HasCustomerSearchResults = false;
            return;
        }
        _customerSearchCts = new CancellationTokenSource();
        _ = DebounceSearchCustomersAsync(value.Trim(), _customerSearchCts.Token);
    }

    private async Task DebounceSearchCustomersAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;
            var url = $"/api/v1/customers?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
            var result = await ApiClient.GetAsync<PagedResponse<CustomerDto>>(url);
            if (ct.IsCancellationRequested) return;
            CustomerSearchResults.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var c in result.Data.Items)
                    CustomerSearchResults.Add(c);
            }
            HasCustomerSearchResults = CustomerSearchResults.Count > 0;
        }
        catch (TaskCanceledException) { }
    }

    /// <summary>Selects a customer from the toolbar typeahead dropdown, switching the page filter.</summary>
    [RelayCommand]
    private void SelectCustomer(CustomerDto? customer)
    {
        if (customer is null) return;
        SelectedCustomer = customer;
        // Populate toolbar without re-triggering typeahead search
        _suppressCustomerSearch = true;
        CustomerSearchText = customer.Name;
        _suppressCustomerSearch = false;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;

        _presetCustomerId = customer.Id;
        PresetCustomerName = customer.Name;
        HasPresetCustomer = true;
        CustomerOutstanding = null;
        CurrentPage = 1;
        _ = LoadPageAsync();
        if (CanViewAccounting)
            _ = FetchOutstandingAsync(customer.Id);
    }

    // ═══ Modal Customer Typeahead (active only in global/all-customers mode) ═══

    partial void OnModalCustomerSearchTextChanged(string value)
    {
        if (_suppressModalSearch) return;
        _modalCustomerSearchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            ModalCustomerSearchResults.Clear();
            HasModalCustomerSearchResults = false;
            return;
        }
        _modalCustomerSearchCts = new CancellationTokenSource();
        _ = DebounceSearchModalCustomersAsync(value.Trim(), _modalCustomerSearchCts.Token);
    }

    private async Task DebounceSearchModalCustomersAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;
            var url = $"/api/v1/customers?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
            var result = await ApiClient.GetAsync<PagedResponse<CustomerDto>>(url);
            if (ct.IsCancellationRequested) return;
            ModalCustomerSearchResults.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var c in result.Data.Items)
                    ModalCustomerSearchResults.Add(c);
            }
            HasModalCustomerSearchResults = ModalCustomerSearchResults.Count > 0;
        }
        catch (TaskCanceledException) { }
    }

    /// <summary>Selects a customer from the modal typeahead dropdown (global mode only).</summary>
    [RelayCommand]
    private void SelectModalCustomer(CustomerDto? customer)
    {
        if (customer is null) return;
        SelectedCustomer = customer;
        ModalCustomerSearchResults.Clear();
        HasModalCustomerSearchResults = false;
        // Update modal text without re-triggering modal search
        _suppressModalSearch = true;
        ModalCustomerSearchText = customer.Name;
        _suppressModalSearch = false;

        // Also update the toolbar to reflect the selection without triggering toolbar search
        _suppressCustomerSearch = true;
        CustomerSearchText = customer.Name;
        _suppressCustomerSearch = false;
        _presetCustomerId = customer.Id;
        PresetCustomerName = customer.Name;
        HasPresetCustomer = true;
        CustomerOutstanding = null;
        CurrentPage = 1;
        _ = LoadPageAsync();
        if (CanViewAccounting)
            _ = FetchOutstandingAsync(customer.Id);
    }

    // ═══ Context Bar Commands ═══

    /// <summary>Clears the customer filter, returning to global/all-customers mode.</summary>
    [RelayCommand]
    private void ClearCustomerFilter()
    {
        _presetCustomerId = null;
        PresetCustomerName = string.Empty;
        HasPresetCustomer = false;
        SelectedCustomer = null;
        // Clear toolbar search field without triggering the global-mode-reset logic
        _suppressCustomerSearch = true;
        CustomerSearchText = string.Empty;
        _suppressCustomerSearch = false;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;
        CustomerOutstanding = null;
        CurrentPage = 1;
        _ = LoadPageAsync();
    }

    // ═══ Outstanding Balance ═══

    private async Task FetchOutstandingAsync(Guid customerId)
    {
        if (!CanViewAccounting) return;
        IsLoadingOutstanding = true;
        CustomerOutstanding = null;
        try
        {
            var url = $"/api/v1/accounting/balances/Customer/{customerId}";
            var result = await ApiClient.GetAsync<PartyOutstandingResponse>(url);
            if (result.IsSuccess && result.Data is not null)
                CustomerOutstanding = result.Data.Outstanding;
            // If call fails (e.g., 403), outstanding simply stays null — UI hides gracefully
        }
        finally
        {
            IsLoadingOutstanding = false;
        }
    }

    // ═══ Print ═══

    [RelayCommand]
    private void PrintPayment(PaymentDto? payment)
    {
        if (payment is null) return;
        DocumentPrintHelper.PrintCustomerPaymentReceipt(payment);
    }

    // ═══ Navigation ═══

    [RelayCommand]
    private void NavigateBackToCustomers()
    {
        _navigationService.NavigateTo<CustomersViewModel>();
    }
}

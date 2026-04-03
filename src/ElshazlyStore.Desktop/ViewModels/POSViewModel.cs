using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Helpers;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class POSViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly IStockChangeNotifier _stockNotifier;
    private readonly ISalesExecutionService _salesExecutionService;

    private readonly List<WarehouseDto> _activeWarehouses = [];
    private CancellationTokenSource? _customerSearchCts;
    private CancellationTokenSource? _customerSuggestionCts;
    private CancellationTokenSource? _submitOverlayCts;
    private decimal _tenderedAmountValue;

    public event Action? BarcodeFocusRequested;

    public POSViewModel(
        ApiClient apiClient,
        IPermissionService permissionService,
        IMessageService messageService,
        IStockChangeNotifier stockNotifier,
        ISalesExecutionService salesExecutionService)
    {
        _apiClient = apiClient;
        _permissionService = permissionService;
        _messageService = messageService;
        _stockNotifier = stockNotifier;
        _salesExecutionService = salesExecutionService;

        Title = Localization.Strings.Nav_SalesPos;
        CanUsePos = _permissionService.HasAllPermissions(
            PermissionCodes.SalesRead,
            PermissionCodes.SalesWrite,
            PermissionCodes.SalesPost);
        CanCreateCustomers = _permissionService.HasPermission(PermissionCodes.CustomersWrite);
        CanPersistPayments = _permissionService.HasPermission(PermissionCodes.PaymentsWrite);
        CanLookupBarcodes = _permissionService.HasPermission(PermissionCodes.ProductsRead);
        CanUseCreditMode = _permissionService.HasAllPermissions(
            PermissionCodes.PaymentsWrite,
            PermissionCodes.AccountingRead);

        SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
        SelectedCashCheckoutMode = CashCheckoutModes.FirstOrDefault();

        Lines.CollectionChanged += OnLinesCollectionChanged;
    }

    [ObservableProperty] private bool _canUsePos;
    [ObservableProperty] private bool _canCreateCustomers;
    [ObservableProperty] private bool _canPersistPayments;
    [ObservableProperty] private bool _canLookupBarcodes;
    [ObservableProperty] private bool _canUseCreditMode;

    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isLookupBusy;
    [ObservableProperty] private bool _isSubmitting;
    [ObservableProperty] private bool _isSubmittingOverlayVisible;

    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";
    [ObservableProperty] private string _formError = string.Empty;

    [ObservableProperty] private DateTime? _invoiceDateUtc = DateTime.UtcNow;
    [ObservableProperty] private string _formNotes = string.Empty;

    [ObservableProperty] private string _barcodeInput = string.Empty;

    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private bool _hasCustomerSearchResults;
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    public ObservableCollection<CustomerDto> CustomerSearchResults { get; } = [];

    [ObservableProperty] private bool _isCustomerQuickAddOpen;
    [ObservableProperty] private string _newCustomerName = string.Empty;
    [ObservableProperty] private string _newCustomerPhone = string.Empty;
    [ObservableProperty] private string _newCustomerError = string.Empty;
    [ObservableProperty] private bool _isSavingCustomer;
    [ObservableProperty] private bool _hasCustomerQuickAddSuggestions;
    public ObservableCollection<CustomerDto> CustomerQuickAddSuggestions { get; } = [];

    [ObservableProperty] private WarehouseDto? _selectedWarehouse;
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    public ObservableCollection<PosLineVm> Lines { get; } = [];

    [ObservableProperty] private PosPaymentMethodOption? _selectedPaymentMethod;
    [ObservableProperty] private string _paymentWalletName = string.Empty;
    [ObservableProperty] private string _paymentReference = string.Empty;
    [ObservableProperty] private string _manualPaymentMethod = string.Empty;
    [ObservableProperty] private string _tenderedAmountText = string.Empty;
    [ObservableProperty] private PosCashCheckoutModeOption? _selectedCashCheckoutMode;
    [ObservableProperty] private WalletProviderOption? _selectedWalletProvider;
    [ObservableProperty] private string _bankWalletName = string.Empty;

    public IReadOnlyList<WalletProviderOption> WalletProviders => WalletProviderOption.All;

    public IReadOnlyList<PosPaymentMethodOption> PaymentMethods { get; } =
    [
        new("Cash", "نقدي"),
        new("Visa", "فيزا"),
        new("EWallet", "محفظة إلكترونية", requiresWalletName: true),
        new("InstaPay", "إنستاباي")
    ];

    public IReadOnlyList<PosCashCheckoutModeOption> CashCheckoutModes { get; } =
    [
        new(PosCashCheckoutMode.FullPayment, Localization.Strings.POS_FullCashModeOption),
        new(PosCashCheckoutMode.PartialCredit, Localization.Strings.POS_PartialCreditModeOption)
    ];

    public decimal InvoiceTotal => Lines
        .Where(line => line.VariantId != Guid.Empty)
        .Sum(line => line.LineTotal);

    public bool HasLines => Lines.Any(line => line.VariantId != Guid.Empty);

    public bool IsAnonymousSale => SelectedCustomer is null;

    public string SaleModeIndicator => IsAnonymousSale
        ? Localization.Strings.POS_SaleModeAnonymous
        : Localization.Strings.POS_SaleModeNamed;

    public bool IsNamedCustomerSale => !IsAnonymousSale;

    public bool IsCashMethod => string.Equals(
        SelectedPaymentMethod?.Value,
        "Cash",
        StringComparison.OrdinalIgnoreCase);

    public bool ShowWalletNameField => SelectedPaymentMethod?.RequiresWalletName == true;

    public bool ShowBankNameField => ShowWalletNameField
                                     && SelectedWalletProvider?.RequiresBankName == true;

    public bool ShowManualMethodField => SelectedPaymentMethod?.IsManual == true;

    public bool ShowCashCheckoutModeSelector => IsNamedCustomerSale;

    public bool ShowNonCashCreditSelector => IsNamedCustomerSale && !IsCashMethod;

    public string InvoiceTotalDisplay => InvoiceNumberFormat.Format(InvoiceTotal);

    public string TenderOutcomeAmountDisplay => InvoiceNumberFormat.Format(TenderOutcomeAmount);

    public bool IsPartialCreditMode => ShowCashCheckoutModeSelector
                                       && SelectedCashCheckoutMode?.Mode == PosCashCheckoutMode.PartialCredit;

    public bool ShowCreditGateHint => ShowCashCheckoutModeSelector && !CanUseCreditMode;

    public string PaymentModeTitle => BuildPaymentModeTitle();

    public string PaymentModeDescription => BuildPaymentModeDescription();

    public string AmountEntryLabel => IsPartialCreditMode
        ? Localization.Strings.POS_PaidNowAmountLabel
        : Localization.Strings.POS_TenderedAmountLabel;

    public bool UsesCashTenderFlow => IsCashMethod || (IsAnonymousSale && SelectedPaymentMethod is null);

    public bool ShowTenderedAmountInput =>
        (UsesCashTenderFlow || (IsNamedCustomerSale && IsPartialCreditMode))
        && InvoiceTotal > 0m;

    public bool HasTenderedAmount => ShowTenderedAmountInput && !string.IsNullOrWhiteSpace(TenderedAmountText);

    public decimal EffectiveTenderedPaymentAmount => Math.Min(_tenderedAmountValue, InvoiceTotal);

    public decimal RemainingAmount => Math.Max(InvoiceTotal - _tenderedAmountValue, 0m);

    public decimal ChangeDueAmount => IsPartialCreditMode
        ? 0m
        : Math.Max(_tenderedAmountValue - InvoiceTotal, 0m);

    public bool HasRemainingAmount => HasTenderedAmount && RemainingAmount > 0m;

    public bool HasChangeDue => HasTenderedAmount && !IsPartialCreditMode && ChangeDueAmount > 0m;

    public bool HasExactTender => HasTenderedAmount && !HasRemainingAmount && !HasChangeDue && InvoiceTotal > 0m;

    public string TenderOutcomeLabel => !HasTenderedAmount
        ? IsPartialCreditMode
            ? Localization.Strings.POS_PaidNowPendingLabel
            : Localization.Strings.POS_TenderPendingLabel
        : HasChangeDue
            ? Localization.Strings.POS_ChangeDueLabel
            : HasRemainingAmount
                ? Localization.Strings.POS_RemainingAmountLabel
                : Localization.Strings.POS_ExactTenderLabel;

    public decimal TenderOutcomeAmount => HasChangeDue
        ? ChangeDueAmount
        : HasRemainingAmount
            ? RemainingAmount
            : InvoiceTotal;

    public bool ShowTenderOutcomeAmount => HasTenderedAmount;

    public string PaymentPersistenceHint => BuildPaymentPersistenceHint();

    public string SelectedCustomerDisplay => SelectedCustomer is null
        ? string.Empty
        : $"{SelectedCustomer.Name} — {SelectedCustomer.Code}";

    partial void OnSelectedPaymentMethodChanged(PosPaymentMethodOption? value)
    {
        EnsureCashCheckoutModeSelection();
        OnPropertyChanged(nameof(IsCashMethod));
        OnPropertyChanged(nameof(ShowCashCheckoutModeSelector));
        OnPropertyChanged(nameof(ShowNonCashCreditSelector));
        OnPropertyChanged(nameof(ShowCreditGateHint));
        OnPropertyChanged(nameof(PaymentModeTitle));
        OnPropertyChanged(nameof(PaymentModeDescription));
        OnPropertyChanged(nameof(AmountEntryLabel));
        OnPropertyChanged(nameof(ShowWalletNameField));
        OnPropertyChanged(nameof(ShowBankNameField));
        OnPropertyChanged(nameof(ShowManualMethodField));

        if (!ShowWalletNameField)
        {
            PaymentWalletName = string.Empty;
            SelectedWalletProvider = null;
            BankWalletName = string.Empty;
        }
        else
        {
            SelectedWalletProvider ??= WalletProviders.FirstOrDefault();
        }

        if (!ShowManualMethodField)
            ManualPaymentMethod = string.Empty;

        RefreshCashTenderState();
    }

    partial void OnSelectedWalletProviderChanged(WalletProviderOption? value)
    {
        OnPropertyChanged(nameof(ShowBankNameField));

        if (value is not null)
            PaymentWalletName = value.Value;

        if (!ShowBankNameField)
            BankWalletName = string.Empty;
    }

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        EnsureCashCheckoutModeSelection();
        OnPropertyChanged(nameof(IsAnonymousSale));
        OnPropertyChanged(nameof(IsNamedCustomerSale));
        OnPropertyChanged(nameof(ShowCashCheckoutModeSelector));
        OnPropertyChanged(nameof(ShowCreditGateHint));
        OnPropertyChanged(nameof(PaymentModeTitle));
        OnPropertyChanged(nameof(PaymentModeDescription));
        OnPropertyChanged(nameof(SaleModeIndicator));
        OnPropertyChanged(nameof(SelectedCustomerDisplay));
        RefreshCashTenderState();
    }

    partial void OnSelectedCashCheckoutModeChanged(PosCashCheckoutModeOption? value)
    {
        OnPropertyChanged(nameof(IsPartialCreditMode));
        OnPropertyChanged(nameof(ShowCreditGateHint));
        OnPropertyChanged(nameof(PaymentModeTitle));
        OnPropertyChanged(nameof(PaymentModeDescription));
        OnPropertyChanged(nameof(AmountEntryLabel));
        RefreshCashTenderState();
    }

    partial void OnTenderedAmountTextChanged(string value)
    {
        if (!NumericInputFoundation.TryParseDecimal(value, 2, autoScaleIntegerInput: false, out var parsed))
            parsed = 0m;

        _tenderedAmountValue = Math.Max(parsed, 0m);
        RefreshCashTenderState();
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (SelectedCustomer is not null && !string.Equals(value, SelectedCustomer.Name, StringComparison.Ordinal))
            SelectedCustomer = null;

        _customerSearchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            CustomerSearchResults.Clear();
            HasCustomerSearchResults = false;
            return;
        }

        _customerSearchCts = new CancellationTokenSource();
        _ = DebounceSearchCustomersAsync(
            value.Trim(),
            CustomerSearchResults,
            hasResults => HasCustomerSearchResults = hasResults,
            _customerSearchCts.Token);
    }

    partial void OnNewCustomerNameChanged(string value) => TriggerQuickAddSuggestionSearch();

    partial void OnNewCustomerPhoneChanged(string value) => TriggerQuickAddSuggestionSearch();

    partial void OnIsSubmittingChanged(bool value)
    {
        _submitOverlayCts?.Cancel();

        if (!value)
        {
            IsSubmittingOverlayVisible = false;
            return;
        }

        _submitOverlayCts = new CancellationTokenSource();
        _ = DelaySubmitOverlayAsync(_submitOverlayCts.Token);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized)
            return;

        await LoadWarehousesAsync();
        IsInitialized = true;

        RequestBarcodeFocus();
    }

    public void ClearTransientStatusForNavigation()
    {
        FormError = string.Empty;
        ClearNotification();
        HasCustomerSearchResults = false;
    }

    [RelayCommand]
    private async Task LoadWarehousesAsync()
    {
        var result = await _apiClient.GetAsync<PagedResponse<WarehouseDto>>("/api/v1/warehouses?page=1&pageSize=500");

        _activeWarehouses.Clear();
        if (result.IsSuccess && result.Data is not null)
        {
            foreach (var warehouse in result.Data.Items.Where(warehouse => warehouse.IsActive))
                _activeWarehouses.Add(warehouse);
        }

        var filtered = SalesWarehousePolicy.BuildSalesWarehouses(_activeWarehouses);

        Warehouses.Clear();
        foreach (var warehouse in filtered)
            Warehouses.Add(warehouse);

        SelectedWarehouse ??= Warehouses.FirstOrDefault(warehouse => warehouse.IsDefault)
                             ?? Warehouses.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ScanBarcodeAsync()
    {
        FormError = string.Empty;

        if (!CanLookupBarcodes)
        {
            FormError = Localization.Strings.POS_BarcodePermissionMissing;
            RequestBarcodeFocus();
            return;
        }

        var barcode = BarcodeInput.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            RequestBarcodeFocus();
            return;
        }

        IsLookupBusy = true;
        var lookupResult = await _apiClient.GetAsync<BarcodeLookupResult>(
            $"/api/v1/barcodes/{Uri.EscapeDataString(barcode)}");
        IsLookupBusy = false;

        if (!lookupResult.IsSuccess || lookupResult.Data is null)
        {
            ShowNotification(lookupResult.ErrorMessage ?? Localization.Strings.Barcode_NotFound, "Error");
            BarcodeInput = string.Empty;
            RequestBarcodeFocus();
            return;
        }

        var lookup = lookupResult.Data;
        if (!lookup.IsActive)
        {
            ShowNotification(Localization.Strings.POS_BarcodeInactiveVariant, "Error");
            BarcodeInput = string.Empty;
            RequestBarcodeFocus();
            return;
        }

        var merged = MergeOrAddScannedLine(lookup);
        BarcodeInput = string.Empty;

        ShowNotification(
            merged
                ? string.Format(Localization.Strings.POS_BarcodeMerged, lookup.Sku)
                : string.Format(Localization.Strings.POS_BarcodeAdded, lookup.Sku),
            "Success");

        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void RemoveLine(PosLineVm? line)
    {
        if (line is null)
            return;

        Lines.Remove(line);
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void ApplyRetailPrice(PosLineVm? line)
    {
        if (line?.RetailPrice is null)
            return;

        line.UnitPrice = line.RetailPrice.Value;
    }

    [RelayCommand]
    private void ApplyWholesalePrice(PosLineVm? line)
    {
        if (line?.WholesalePrice is null)
            return;

        line.UnitPrice = line.WholesalePrice.Value;
    }

    [RelayCommand]
    private void SplitSingleUnit(PosLineVm? line)
    {
        FormError = string.Empty;

        if (line is null || line.Quantity <= 1)
            return;

        if (!line.IsWholeQuantity)
        {
            FormError = Localization.Strings.POS_SplitRequiresWholeQuantity;
            return;
        }

        var lineIndex = Lines.IndexOf(line);
        if (lineIndex < 0)
            return;

        var originalQty = line.Quantity;
        const decimal splitQty = 1m;
        var (splitDiscount, remainingDiscount) = SplitDiscount(line.DiscountAmount, originalQty, splitQty);

        line.Quantity = originalQty - splitQty;
        line.DiscountAmount = remainingDiscount;

        var splitLine = line.Clone(splitQty, splitDiscount);
        Lines.Insert(lineIndex + 1, splitLine);

        ShowNotification(Localization.Strings.POS_SplitSingleDone, "Info");
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void SplitAllUnits(PosLineVm? line)
    {
        FormError = string.Empty;

        if (line is null || line.Quantity <= 1)
            return;

        if (!line.IsWholeQuantity)
        {
            FormError = Localization.Strings.POS_SplitRequiresWholeQuantity;
            return;
        }

        var lineIndex = Lines.IndexOf(line);
        if (lineIndex < 0)
            return;

        var unitCount = (int)line.Quantity;
        if (unitCount <= 1)
            return;

        var baseDiscount = unitCount == 0
            ? 0m
            : decimal.Round(line.DiscountAmount / unitCount, 4, MidpointRounding.AwayFromZero);
        var distributedDiscount = baseDiscount * unitCount;
        var firstLineExtraDiscount = line.DiscountAmount - distributedDiscount;

        var splitLines = new List<PosLineVm>(unitCount);
        for (var i = 0; i < unitCount; i++)
        {
            var discount = baseDiscount + (i == 0 ? firstLineExtraDiscount : 0m);
            splitLines.Add(line.Clone(1m, discount));
        }

        Lines.RemoveAt(lineIndex);
        for (var i = 0; i < splitLines.Count; i++)
            Lines.Insert(lineIndex + i, splitLines[i]);

        ShowNotification(Localization.Strings.POS_SplitAllDone, "Info");
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void ClearBasket()
    {
        FormError = string.Empty;
        Lines.Clear();
        ShowNotification(Localization.Strings.POS_BasketCleared, "Info");
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void SetAnonymousSale()
    {
        SelectedCustomer = null;
        CustomerSearchText = string.Empty;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void SelectCustomer(CustomerDto? customer)
    {
        if (customer is null)
            return;

        SelectedCustomer = customer;
        CustomerSearchText = customer.Name;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void OpenCustomerQuickAdd()
    {
        NewCustomerError = string.Empty;
        NewCustomerName = string.IsNullOrWhiteSpace(CustomerSearchText)
            ? string.Empty
            : CustomerSearchText.Trim();
        NewCustomerPhone = string.Empty;
        IsCustomerQuickAddOpen = true;
        TriggerQuickAddSuggestionSearch();
    }

    [RelayCommand]
    private void CloseCustomerQuickAdd()
    {
        IsCustomerQuickAddOpen = false;
        NewCustomerError = string.Empty;
        CustomerQuickAddSuggestions.Clear();
        HasCustomerQuickAddSuggestions = false;
    }

    [RelayCommand]
    private void UseQuickAddSuggestion(CustomerDto? customer)
    {
        if (customer is null)
            return;

        SelectCustomer(customer);
        CloseCustomerQuickAdd();
    }

    [RelayCommand]
    private async Task SaveNewCustomerAsync()
    {
        NewCustomerError = string.Empty;

        if (string.IsNullOrWhiteSpace(NewCustomerName))
        {
            NewCustomerError = Localization.Strings.Validation_NameRequired;
            return;
        }

        IsSavingCustomer = true;
        try
        {
            var body = new CreateCustomerRequest
            {
                Name = NewCustomerName.Trim(),
                Phone = string.IsNullOrWhiteSpace(NewCustomerPhone) ? null : NewCustomerPhone.Trim()
            };

            var result = await _apiClient.PostAsync<CustomerDto>("/api/v1/customers", body);
            if (!result.IsSuccess || result.Data is null)
            {
                NewCustomerError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                return;
            }

            SelectCustomer(result.Data);
            ShowNotification(Localization.Strings.Sales_CustomerCreatedPendingInvoiceSave, "Info");
            CloseCustomerQuickAdd();
            RequestBarcodeFocus();
        }
        finally
        {
            IsSavingCustomer = false;
        }
    }

    [RelayCommand]
    private async Task CompleteCheckoutAsync()
    {
        FormError = string.Empty;

        if (SelectedWarehouse is null)
        {
            FormError = Localization.Strings.Validation_WarehouseRequired;
            return;
        }

        var validLines = Lines.Where(line => line.VariantId != Guid.Empty).ToList();
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

            if (line.UnitPrice < 0)
            {
                FormError = Localization.Strings.Sales_UnitPriceInvalid;
                return;
            }

            if (line.DiscountAmount < 0)
            {
                FormError = Localization.Strings.Sales_DiscountInvalid;
                return;
            }

            if (line.DiscountAmount > line.Quantity * line.UnitPrice)
            {
                FormError = Localization.Strings.Sales_DiscountExceedsLine;
                return;
            }
        }

        if (IsNamedCustomerSale)
        {
            if (!CanPersistPayments)
            {
                FormError = Localization.Strings.POS_PaymentPermissionMissing;
                return;
            }

            if (SelectedPaymentMethod is null)
            {
                FormError = Localization.Strings.POS_PaymentMethodRequired;
                return;
            }

            if (ShowWalletNameField && SelectedWalletProvider is null)
            {
                FormError = Localization.Strings.POS_WalletNameRequired;
                return;
            }

            if (ShowBankNameField && string.IsNullOrWhiteSpace(BankWalletName))
            {
                FormError = Localization.Strings.POS_WalletNameRequired;
                return;
            }

            if (ShowManualMethodField && string.IsNullOrWhiteSpace(ManualPaymentMethod))
            {
                FormError = Localization.Strings.POS_ManualMethodRequired;
                return;
            }
        }

        if (ShowTenderedAmountInput && HasTenderedAmount && _tenderedAmountValue <= 0m)
        {
            FormError = Localization.Strings.POS_TenderedAmountInvalid;
            return;
        }

        if (IsAnonymousSale && HasRemainingAmount)
        {
            FormError = Localization.Strings.POS_AnonymousRemainingNotAllowed;
            return;
        }

        if (IsNamedCustomerSale)
        {
            if (IsPartialCreditMode)
            {
                if (!CanUseCreditMode)
                {
                    FormError = Localization.Strings.POS_CreditModePermissionMissing;
                    return;
                }

                if (!HasTenderedAmount)
                {
                    FormError = Localization.Strings.POS_PaidAmountRequired;
                    return;
                }

                if (_tenderedAmountValue >= InvoiceTotal)
                {
                    FormError = Localization.Strings.POS_PartialModeRequiresRemaining;
                    return;
                }
            }
            else if (IsCashMethod && HasRemainingAmount)
            {
                FormError = CanUseCreditMode
                    ? Localization.Strings.POS_SwitchToPartialForRemaining
                    : Localization.Strings.POS_FullCashRequiresCompleteTender;
                return;
            }
        }

        IsSubmitting = true;
        try
        {
            var request = new SalesCheckoutExecutionRequest
            {
                WarehouseId = SelectedWarehouse.Id,
                CustomerId = SelectedCustomer?.Id,
                InvoiceDateUtc = InvoiceDateUtc,
                Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                Lines = validLines.Select(line => new SaleLineRequest
                {
                    VariantId = line.VariantId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount
                }).ToList(),
                CreatePaymentForNamedCustomer = IsNamedCustomerSale,
                PaymentMethod = ResolvePaymentMethodForRequest(),
                PaymentAmount = ResolvePaymentAmountForRequest(),
                WalletName = ResolveEffectiveWalletName(),
                Reference = string.IsNullOrWhiteSpace(PaymentReference) ? null : PaymentReference.Trim(),
                PaymentDateUtc = DateTime.UtcNow
            };

            var result = await _salesExecutionService.ExecuteImmediateCheckoutAsync(request);

            if (!result.IsSaleCreated)
            {
                FormError = result.SaleCreateErrorMessage ?? Localization.Strings.State_UnexpectedError;
                return;
            }

            if (!result.IsSalePosted)
            {
                FormError = result.SalePostErrorMessage ?? Localization.Strings.State_UnexpectedError;
                return;
            }

            _stockNotifier.NotifyStockChanged();

            if (result.SaleFresh is not null)
            {
                DocumentPrintHelper.PrintSale(result.SaleFresh, BuildPrintTraceOverride(result));
            }
            else if (!string.IsNullOrWhiteSpace(result.SaleFetchErrorMessage))
            {
                _messageService.ShowError(Localization.Strings.POS_PrintFetchFailed);
            }

            var checkoutMessage = BuildCheckoutMessage(result);
            var notificationType = ResolveCheckoutNotificationType(result);

            ShowNotification(checkoutMessage, notificationType);
            ResetAfterSuccessfulCheckout();
            RequestBarcodeFocus();
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private bool MergeOrAddScannedLine(BarcodeLookupResult lookup)
    {
        var existing = Lines.FirstOrDefault(line => line.VariantId == lookup.VariantId);
        if (existing is not null)
        {
            existing.Quantity += 1m;
            if (existing.UnitPrice <= 0)
                existing.UnitPrice = ResolveDefaultUnitPrice(lookup);
            return true;
        }

        Lines.Add(new PosLineVm
        {
            VariantId = lookup.VariantId,
            VariantSku = lookup.Sku,
            Barcode = lookup.Barcode,
            ProductName = lookup.ProductName,
            VariantDisplay = BuildVariantDisplay(lookup.ProductName, lookup.Sku, lookup.Color, lookup.Size),
            Quantity = 1m,
            RetailPrice = lookup.RetailPrice,
            WholesalePrice = lookup.WholesalePrice,
            UnitPrice = ResolveDefaultUnitPrice(lookup)
        });

        return false;
    }

    private static decimal ResolveDefaultUnitPrice(BarcodeLookupResult lookup)
        => lookup.RetailPrice ?? lookup.WholesalePrice ?? 0m;

    private static (decimal splitDiscount, decimal remainingDiscount) SplitDiscount(
        decimal totalDiscount,
        decimal originalQty,
        decimal splitQty)
    {
        if (totalDiscount <= 0 || originalQty <= 0 || splitQty <= 0)
            return (0m, totalDiscount);

        var splitDiscount = decimal.Round(
            totalDiscount * (splitQty / originalQty),
            4,
            MidpointRounding.AwayFromZero);

        var remainingDiscount = totalDiscount - splitDiscount;
        if (remainingDiscount < 0)
        {
            splitDiscount += remainingDiscount;
            remainingDiscount = 0m;
        }

        return (splitDiscount, remainingDiscount);
    }

    private async Task DebounceSearchCustomersAsync(
        string query,
        ObservableCollection<CustomerDto> target,
        Action<bool> setHasResults,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested)
                return;

            var url = $"/api/v1/customers?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
            var result = await _apiClient.GetAsync<PagedResponse<CustomerDto>>(url, ct);
            if (ct.IsCancellationRequested)
                return;

            target.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var customer in result.Data.Items.Where(customer => customer.IsActive))
                    target.Add(customer);
            }

            setHasResults(target.Count > 0);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void TriggerQuickAddSuggestionSearch()
    {
        _customerSuggestionCts?.Cancel();

        if (!IsCustomerQuickAddOpen)
            return;

        var query = ResolveQuickAddSuggestionQuery();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            CustomerQuickAddSuggestions.Clear();
            HasCustomerQuickAddSuggestions = false;
            return;
        }

        _customerSuggestionCts = new CancellationTokenSource();
        _ = DebounceSearchCustomersAsync(
            query,
            CustomerQuickAddSuggestions,
            hasResults => HasCustomerQuickAddSuggestions = hasResults,
            _customerSuggestionCts.Token);
    }

    private string ResolveQuickAddSuggestionQuery()
    {
        var name = NewCustomerName.Trim();
        var phone = NewCustomerPhone.Trim();
        return phone.Length >= name.Length ? phone : name;
    }

    private string? ResolvePaymentMethodForRequest()
    {
        if (!IsNamedCustomerSale)
            return null;

        if (SelectedPaymentMethod is null)
            return "Cash";

        return SelectedPaymentMethod.IsManual
            ? ManualPaymentMethod.Trim()
            : SelectedPaymentMethod.Value;
    }

    private decimal? ResolvePaymentAmountForRequest()
    {
        if (!IsNamedCustomerSale)
            return null;

        if (IsPartialCreditMode && HasTenderedAmount)
            return _tenderedAmountValue;

        if (IsCashMethod)
        {
            if (!HasTenderedAmount)
                return InvoiceTotal;
            return EffectiveTenderedPaymentAmount;
        }

        return InvoiceTotal;
    }

    private void ResetAfterSuccessfulCheckout()
    {
        Lines.Clear();
        FormNotes = string.Empty;
        PaymentReference = string.Empty;
        PaymentWalletName = string.Empty;
        ManualPaymentMethod = string.Empty;
        TenderedAmountText = string.Empty;
        _tenderedAmountValue = 0m;
        InvoiceDateUtc = DateTime.UtcNow;
        BarcodeInput = string.Empty;
        SelectedWalletProvider = null;
        BankWalletName = string.Empty;

        if (SelectedPaymentMethod is null)
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault();

        SelectedCashCheckoutMode = CashCheckoutModes.FirstOrDefault();

        RefreshCashTenderState();
    }

    private static string BuildVariantDisplay(string? productName, string sku, string? color = null, string? size = null)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!);
        if (!string.IsNullOrWhiteSpace(size)) parts.Add(size!);

        var meta = parts.Count > 0 ? string.Join(" / ", parts) : null;
        return string.IsNullOrWhiteSpace(meta)
            ? sku
            : $"{sku} — {meta}";
    }

    private static string ResolveCheckoutNotificationType(SalesCheckoutExecutionResult result)
    {
        if (result.IsPaymentAttempted && !result.IsPaymentPersisted)
            return "Warning";

        return "Success";
    }

    private string BuildCheckoutMessage(SalesCheckoutExecutionResult result)
    {
        if (result.IsAnonymousSale)
        {
            return HasChangeDue
                ? string.Format(Localization.Strings.POS_CheckoutPostedAnonymousChangeDue, InvoiceNumberFormat.Format(ChangeDueAmount))
                : Localization.Strings.POS_CheckoutPostedAnonymousNoPayment;
        }

        if (result.IsPaymentAttempted && result.IsPaymentPersisted)
        {
            if (HasRemainingAmount)
            {
                return string.Format(
                    Localization.Strings.POS_CheckoutPostedPaymentPartial,
                    InvoiceNumberFormat.Format(EffectiveTenderedPaymentAmount),
                    InvoiceNumberFormat.Format(RemainingAmount));
            }

            if (HasChangeDue)
            {
                return string.Format(
                    Localization.Strings.POS_CheckoutPostedPaymentSavedWithChange,
                    InvoiceNumberFormat.Format(result.InvoiceTotalAmount),
                    InvoiceNumberFormat.Format(ChangeDueAmount));
            }

            return Localization.Strings.POS_CheckoutPostedPaymentSaved;
        }

        if (result.IsPaymentAttempted && !result.IsPaymentPersisted)
        {
            return Localization.Strings.POS_CheckoutPostedPaymentFailed;
        }

        return Localization.Strings.POS_CheckoutPosted;
    }

    private string BuildPaymentPersistenceHint()
    {
        if (IsAnonymousSale)
            return Localization.Strings.POS_PersistenceHintAnonymous;

        if (!IsCashMethod)
            return Localization.Strings.POS_PersistenceHintNamedNonCash;

        if (ShowCreditGateHint)
            return Localization.Strings.POS_CreditModePermissionHint;

        if (!UsesCashTenderFlow)
            return Localization.Strings.POS_PersistenceHintNamedExact;

        if (HasRemainingAmount)
            return Localization.Strings.POS_PersistenceHintNamedPartial;

        if (HasChangeDue)
            return Localization.Strings.POS_PersistenceHintNamedCashChange;

        return Localization.Strings.POS_PersistenceHintNamedExact;
    }

    private void RefreshCashTenderState()
    {
        OnPropertyChanged(nameof(UsesCashTenderFlow));
        OnPropertyChanged(nameof(IsPartialCreditMode));
        OnPropertyChanged(nameof(ShowCashCheckoutModeSelector));
        OnPropertyChanged(nameof(ShowNonCashCreditSelector));
        OnPropertyChanged(nameof(ShowCreditGateHint));
        OnPropertyChanged(nameof(PaymentModeTitle));
        OnPropertyChanged(nameof(PaymentModeDescription));
        OnPropertyChanged(nameof(AmountEntryLabel));
        OnPropertyChanged(nameof(ShowTenderedAmountInput));
        OnPropertyChanged(nameof(HasTenderedAmount));
        OnPropertyChanged(nameof(EffectiveTenderedPaymentAmount));
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(ChangeDueAmount));
        OnPropertyChanged(nameof(HasRemainingAmount));
        OnPropertyChanged(nameof(HasChangeDue));
        OnPropertyChanged(nameof(HasExactTender));
        OnPropertyChanged(nameof(TenderOutcomeLabel));
        OnPropertyChanged(nameof(TenderOutcomeAmount));
        OnPropertyChanged(nameof(TenderOutcomeAmountDisplay));
        OnPropertyChanged(nameof(ShowTenderOutcomeAmount));
        OnPropertyChanged(nameof(PaymentPersistenceHint));
        OnPropertyChanged(nameof(InvoiceTotalDisplay));
    }

    private async Task DelaySubmitOverlayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(180, ct);
            if (!ct.IsCancellationRequested && IsSubmitting)
                IsSubmittingOverlayVisible = true;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (PosLineVm line in e.OldItems)
                line.PropertyChanged -= OnLinePropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (PosLineVm line in e.NewItems)
                line.PropertyChanged += OnLinePropertyChanged;
        }

        OnPropertyChanged(nameof(InvoiceTotal));
        OnPropertyChanged(nameof(InvoiceTotalDisplay));
        OnPropertyChanged(nameof(HasLines));
        RefreshCashTenderState();
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(InvoiceTotal));
        OnPropertyChanged(nameof(InvoiceTotalDisplay));
        OnPropertyChanged(nameof(HasLines));
        RefreshCashTenderState();
    }

    private void ShowNotification(string message, string type)
    {
        // If the same message is set again, CommunityToolkit won't fire PropertyChanged.
        // Force a change so the NotificationBar control always picks it up.
        if (NotificationMessage == message)
            NotificationMessage = string.Empty;
        NotificationMessage = message;
        NotificationType = type;
    }

    private void ClearNotification()
    {
        NotificationMessage = string.Empty;
        NotificationType = "Info";
    }

    private void EnsureCashCheckoutModeSelection()
    {
        if (SelectedCashCheckoutMode is null)
        {
            SelectedCashCheckoutMode = CashCheckoutModes.FirstOrDefault();
            return;
        }

        if (!ShowCashCheckoutModeSelector)
        {
            SelectedCashCheckoutMode = CashCheckoutModes.FirstOrDefault();
            return;
        }

        if (!CanUseCreditMode && SelectedCashCheckoutMode.Mode == PosCashCheckoutMode.PartialCredit)
            SelectedCashCheckoutMode = CashCheckoutModes.FirstOrDefault();
    }

    private string BuildPaymentModeTitle()
    {
        if (IsAnonymousSale)
            return Localization.Strings.POS_ModeAnonymousTitle;

        if (!IsCashMethod)
            return Localization.Strings.POS_ModeNamedNonCashTitle;

        return IsPartialCreditMode
            ? Localization.Strings.POS_ModePartialCreditTitle
            : Localization.Strings.POS_ModeFullCashTitle;
    }

    private string BuildPaymentModeDescription()
    {
        if (IsAnonymousSale)
            return Localization.Strings.POS_ModeAnonymousDescription;

        if (!IsCashMethod)
            return Localization.Strings.POS_ModeNamedNonCashDescription;

        return IsPartialCreditMode
            ? Localization.Strings.POS_ModePartialCreditDescription
            : Localization.Strings.POS_ModeFullCashDescription;
    }

    private SalePaymentTraceDto? BuildPrintTraceOverride(SalesCheckoutExecutionResult result)
    {
        if (!result.IsAnonymousSale)
            return null;

        var walletName = ResolveEffectiveWalletName();

        return new SalePaymentTraceDto
        {
            PaidAmount = HasTenderedAmount ? Math.Min(_tenderedAmountValue, InvoiceTotal) : null,
            RemainingAmount = HasTenderedAmount ? RemainingAmount : null,
            PaymentMethod = SelectedPaymentMethod?.Value,
            WalletName = walletName,
            PaymentReference = string.IsNullOrWhiteSpace(PaymentReference)
                ? null
                : PaymentReference.Trim(),
            PaymentCount = 0,
            IsOperationalOnly = true,
            Note = Localization.Strings.POS_PrintAnonymousOperationalNote
        };
    }

    private string? ResolveEffectiveWalletName()
    {
        if (!ShowWalletNameField)
            return null;

        if (SelectedWalletProvider is null)
            return null;

        if (SelectedWalletProvider.RequiresBankName && !string.IsNullOrWhiteSpace(BankWalletName))
            return $"{SelectedWalletProvider.Value} — {BankWalletName.Trim()}";

        return SelectedWalletProvider.Value;
    }

    private void RequestBarcodeFocus() => BarcodeFocusRequested?.Invoke();
}

public sealed partial class PosLineVm : ObservableObject
{
    [ObservableProperty] private Guid _variantId;
    [ObservableProperty] private string _variantSku = string.Empty;
    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _variantDisplay = string.Empty;

    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _discountAmount;

    [ObservableProperty] private decimal? _retailPrice;
    [ObservableProperty] private decimal? _wholesalePrice;

    public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;

    public string LineTotalDisplay => InvoiceNumberFormat.Format(LineTotal);

    public bool IsWholeQuantity => Quantity == decimal.Truncate(Quantity);

    public bool CanSplit => Quantity > 1m && IsWholeQuantity;

    public PosLineVm Clone(decimal quantity, decimal discountAmount)
    {
        return new PosLineVm
        {
            VariantId = VariantId,
            VariantSku = VariantSku,
            Barcode = Barcode,
            ProductName = ProductName,
            VariantDisplay = VariantDisplay,
            Quantity = quantity,
            UnitPrice = UnitPrice,
            DiscountAmount = discountAmount,
            RetailPrice = RetailPrice,
            WholesalePrice = WholesalePrice
        };
    }

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LineTotalDisplay));
        OnPropertyChanged(nameof(IsWholeQuantity));
        OnPropertyChanged(nameof(CanSplit));
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LineTotalDisplay));
    }

    partial void OnDiscountAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LineTotalDisplay));
    }
}

public sealed class PosPaymentMethodOption
{
    public PosPaymentMethodOption(string value, string displayName, bool requiresWalletName = false, bool isManual = false)
    {
        Value = value;
        DisplayName = displayName;
        RequiresWalletName = requiresWalletName;
        IsManual = isManual;
    }

    public string Value { get; }

    public string DisplayName { get; }

    public bool RequiresWalletName { get; }

    public bool IsManual { get; }
}

public sealed class PosCashCheckoutModeOption
{
    public PosCashCheckoutModeOption(PosCashCheckoutMode mode, string displayName)
    {
        Mode = mode;
        DisplayName = displayName;
    }

    public PosCashCheckoutMode Mode { get; }

    public string DisplayName { get; }
}

public enum PosCashCheckoutMode
{
    FullPayment = 0,
    PartialCredit = 1,
}

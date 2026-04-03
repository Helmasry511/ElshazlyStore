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

public sealed partial class SalesViewModel : PagedListViewModelBase<SaleDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly IStockChangeNotifier _stockNotifier;
    private readonly ISalesExecutionService _salesExecutionService;
    private readonly List<WarehouseDto> _activeWarehouses = [];
    private CancellationTokenSource? _variantSearchCts;
    private CancellationTokenSource? _customerSearchCts;
    private CancellationTokenSource? _customerSuggestionCts;
    private SalesLineVm? _editingLine;
    private Guid? _editingId;
    private bool _hasPendingQuickAddCustomerSuccess;

    public SalesViewModel(
        ApiClient apiClient,
        IPermissionService permissionService,
        IMessageService messageService,
        IStockChangeNotifier stockNotifier,
        ISalesExecutionService salesExecutionService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        _stockNotifier = stockNotifier;
        _salesExecutionService = salesExecutionService;

        Title = Localization.Strings.Nav_Sales;
        CanWrite = _permissionService.HasPermission(PermissionCodes.SalesWrite);
        CanPost = _permissionService.HasPermission(PermissionCodes.SalesPost);
        CanCreateCustomers = _permissionService.HasPermission(PermissionCodes.CustomersWrite);
        SortColumn = "date";
        SortDescending = true;

        Lines.CollectionChanged += OnLinesCollectionChanged;
    }

    [ObservableProperty] private bool _canWrite;
    [ObservableProperty] private bool _canPost;
    [ObservableProperty] private bool _canCreateCustomers;

    [ObservableProperty] private bool _isInitialized;

    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";

    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private SaleDto? _selectedSale;

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _formInvoiceNumber = string.Empty;
    [ObservableProperty] private DateTime? _formInvoiceDate = DateTime.UtcNow;
    [ObservableProperty] private string _formNotes = string.Empty;
    [ObservableProperty] private string _formInfo = string.Empty;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _formStatus = string.Empty;

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

    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];
    [ObservableProperty] private WarehouseDto? _selectedWarehouse;

    [ObservableProperty] private string _variantSearchText = string.Empty;
    [ObservableProperty] private bool _hasVariantSearchResults;
    [ObservableProperty] private bool _isVariantSearchOpen;
    [ObservableProperty] private bool _isVariantSearchLoading;
    [ObservableProperty] private string _variantSearchNote = string.Empty;
    public ObservableCollection<VariantListDto> VariantSearchResults { get; } = [];

    public ObservableCollection<SalesLineVm> Lines { get; } = [];

    public decimal InvoiceTotal => Lines
        .Where(line => line.VariantId != Guid.Empty)
        .Sum(line => line.LineTotal);

    public string FormInvoiceNumberDisplay => string.IsNullOrWhiteSpace(FormInvoiceNumber)
        ? Localization.Strings.Sales_InvoiceNumberPending
        : FormInvoiceNumber;

    public bool IsCustomerSelected => SelectedCustomer is not null;

    public string SelectedCustomerDisplay => SelectedCustomer is null
        ? string.Empty
        : $"{SelectedCustomer.Name} — {SelectedCustomer.Code}";

    protected override Task<ApiResult<PagedResponse<SaleDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/sales", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<SaleDto>>(url);
    }

    partial void OnFormInvoiceNumberChanged(string value) => OnPropertyChanged(nameof(FormInvoiceNumberDisplay));

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        OnPropertyChanged(nameof(IsCustomerSelected));
        OnPropertyChanged(nameof(SelectedCustomerDisplay));
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
            results => HasCustomerSearchResults = results,
            _customerSearchCts.Token);
    }

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

    partial void OnNewCustomerNameChanged(string value) => TriggerQuickAddSuggestionSearch();

    partial void OnNewCustomerPhoneChanged(string value) => TriggerQuickAddSuggestionSearch();

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await LoadWarehousesAsync();
        IsInitialized = true;
    }

    private async Task LoadWarehousesAsync()
    {
        var result = await ApiClient.GetAsync<PagedResponse<WarehouseDto>>("/api/v1/warehouses?page=1&pageSize=500");

        _activeWarehouses.Clear();
        if (result.IsSuccess && result.Data is not null)
        {
            foreach (var warehouse in result.Data.Items.Where(warehouse => warehouse.IsActive))
                _activeWarehouses.Add(warehouse);
        }

        ApplySalesWarehouseFilter();
    }

    [RelayCommand]
    private async Task OpenDetailAsync(SaleDto? sale)
    {
        if (sale is null) return;

        IsBusy = true;
        var result = await ApiClient.GetAsync<SaleDto>($"/api/v1/sales/{sale.Id}");
        IsBusy = false;

        if (result.IsSuccess && result.Data is not null)
        {
            SelectedSale = result.Data;
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
        SelectedSale = null;
    }

    [RelayCommand]
    private async Task OpenCreateAsync()
    {
        ResetForm();
        IsEditMode = false;
        FormStatus = Localization.Strings.Status_Draft;
        FormInvoiceDate = DateTime.UtcNow;

        if (_activeWarehouses.Count == 0)
            await LoadWarehousesAsync();
        else
            ApplySalesWarehouseFilter();

        SelectedWarehouse = Warehouses.FirstOrDefault(warehouse => warehouse.IsDefault) ?? Warehouses.FirstOrDefault();
        IsEditing = true;
    }

    [RelayCommand]
    private async Task OpenEditAsync(SaleDto? sale)
    {
        if (sale is null) return;
        if (sale.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.Sales_CannotEditPosted);
            return;
        }

        IsBusy = true;
        var result = await ApiClient.GetAsync<SaleDto>($"/api/v1/sales/{sale.Id}");
        IsBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        var detail = result.Data;
        ResetForm();
        IsEditMode = true;
        _editingId = detail.Id;
        FormInvoiceNumber = detail.InvoiceNumber;
        FormInvoiceDate = detail.InvoiceDateUtc;
        FormNotes = detail.Notes ?? string.Empty;
        FormStatus = detail.StatusDisplay;

        ApplySalesWarehouseFilter(detail.WarehouseId);
        SelectedWarehouse = Warehouses.FirstOrDefault(warehouse => warehouse.Id == detail.WarehouseId);

        if (detail.CustomerId.HasValue)
        {
            SelectedCustomer = new CustomerDto
            {
                Id = detail.CustomerId.Value,
                Name = detail.CustomerName ?? string.Empty,
                Code = string.Empty,
                IsActive = true
            };
            CustomerSearchText = detail.CustomerName ?? string.Empty;
        }

        Lines.Clear();
        if (detail.Lines is { Count: > 0 })
        {
            foreach (var line in detail.Lines)
            {
                var vm = new SalesLineVm
                {
                    VariantId = line.VariantId,
                    VariantSku = line.Sku,
                    ProductName = line.ProductName ?? string.Empty,
                    VariantDisplay = BuildVariantDisplay(line.ProductName, line.Sku),
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount
                };

                Lines.Add(vm);
            }

            await HydrateLineVariantPricingAsync(Lines);
        }

        if (Lines.Count == 0)
            AddLine();

        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetPendingQuickAddCustomerState();
        CloseVariantSearch();
        CloseCustomerQuickAdd();
        IsEditing = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        FormError = string.Empty;
        ClearNotification();

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

        IsSaving = true;
        try
        {
            var successMessage = string.Empty;
            var lineRequests = validLines.Select(line => new SaleLineRequest
            {
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.DiscountAmount
            }).ToList();

            if (_editingId is null)
            {
                var body = new CreateSaleRequest
                {
                    WarehouseId = SelectedWarehouse.Id,
                    CustomerId = SelectedCustomer?.Id,
                    InvoiceDateUtc = NormalizeInvoiceDateForRequest(FormInvoiceDate),
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };

                var result = await ApiClient.PostAsync<SaleDto>("/api/v1/sales", body);
                if (!result.IsSuccess)
                {
                    FormError = BuildSaveFailureMessage(result.ErrorMessage);
                    return;
                }

                successMessage = Localization.Strings.Sales_Created;
            }
            else
            {
                var body = new UpdateSaleRequest
                {
                    WarehouseId = SelectedWarehouse.Id,
                    CustomerId = SelectedCustomer?.Id,
                    ClearCustomer = SelectedCustomer is null,
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    Lines = lineRequests
                };

                var result = await ApiClient.PutAsync<SaleDto>($"/api/v1/sales/{_editingId}", body);
                if (!result.IsSuccess)
                {
                    FormError = BuildSaveFailureMessage(result.ErrorMessage);
                    return;
                }

                successMessage = Localization.Strings.Sales_Updated;
            }

            ResetPendingQuickAddCustomerState();
            IsEditing = false;
            await LoadPageAsync();
            ShowNotification(successMessage, "Success");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task PostSaleAsync(SaleDto? sale)
    {
        if (sale is null) return;
        if (sale.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.Sales_AlreadyPosted);
            return;
        }

        if (!_messageService.ShowConfirm(Localization.Strings.Sales_ConfirmPost))
            return;

        IsBusy = true;
        var result = await _salesExecutionService.PostDraftSaleAsync(sale.Id);
        IsBusy = false;

        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        ShowNotification(Localization.Strings.Sales_PostSuccess, "Success");
        _stockNotifier.NotifyStockChanged();
        await LoadPageAsync();

        if (IsDetailOpen && SelectedSale?.Id == sale.Id)
            await OpenDetailAsync(sale);
    }

    [RelayCommand]
    private async Task DeleteSaleAsync(SaleDto? sale)
    {
        if (sale is null) return;
        if (sale.Status != "Draft")
        {
            _messageService.ShowError(Localization.Strings.Sales_CannotDeletePosted);
            return;
        }

        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDelete))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/sales/{sale.Id}");
        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        ShowNotification(Localization.Strings.Sales_DeleteSuccess, "Success");

        if (IsDetailOpen && SelectedSale?.Id == sale.Id)
            CloseDetail();

        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task PrintSaleDocAsync(SaleDto? sale)
    {
        if (sale is null) return;

        IsBusy = true;
        var result = await _salesExecutionService.FetchSaleAsync(sale.Id);
        IsBusy = false;

        if (result.IsSuccess && result.Data is not null)
        {
            DocumentPrintHelper.PrintSale(result.Data);
        }
        else
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
        }
    }

    [RelayCommand]
    private async Task ApplyGridSortAsync(string? sortColumn)
    {
        if (string.IsNullOrWhiteSpace(sortColumn))
            return;

        if (string.Equals(SortColumn, sortColumn, StringComparison.Ordinal))
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = sortColumn;
            SortDescending = false;
        }

        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    private void AddLine()
    {
        Lines.Add(new SalesLineVm { Quantity = 1m });
    }

    [RelayCommand]
    private void RemoveLine(SalesLineVm? line)
    {
        if (line is not null)
            Lines.Remove(line);
    }

    [RelayCommand]
    private void StartLineVariantSearch(SalesLineVm? line)
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
        _editingLine.ProductName = variant.ProductName;
        _editingLine.VariantSku = variant.Sku;
        _editingLine.VariantDisplay = BuildVariantDisplay(variant.ProductName, variant.Sku, variant.Color, variant.Size);
        _editingLine.RetailPrice = variant.RetailPrice;
        _editingLine.WholesalePrice = variant.WholesalePrice;
        if (_editingLine.Quantity <= 0)
            _editingLine.Quantity = 1m;
        if (_editingLine.UnitPrice <= 0)
            _editingLine.UnitPrice = variant.RetailPrice ?? variant.WholesalePrice ?? 0m;

        CloseVariantSearch();
    }

    [RelayCommand]
    private void ApplyRetailPrice(SalesLineVm? line)
    {
        if (line?.RetailPrice is null) return;
        line.UnitPrice = line.RetailPrice.Value;
    }

    [RelayCommand]
    private void ApplyWholesalePrice(SalesLineVm? line)
    {
        if (line?.WholesalePrice is null) return;
        line.UnitPrice = line.WholesalePrice.Value;
    }

    [RelayCommand]
    private void SelectCustomer(CustomerDto? customer)
    {
        if (customer is null) return;

        SelectedCustomer = customer;
        CustomerSearchText = customer.Name;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;
    }

    [RelayCommand]
    private void ClearCustomerSelection()
    {
        SelectedCustomer = null;
        CustomerSearchText = string.Empty;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;
    }

    [RelayCommand]
    private void OpenCustomerQuickAdd()
    {
        NewCustomerError = string.Empty;
        NewCustomerName = string.IsNullOrWhiteSpace(CustomerSearchText) ? string.Empty : CustomerSearchText.Trim();
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
        if (customer is null) return;

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

            var result = await ApiClient.PostAsync<CustomerDto>("/api/v1/customers", body);
            if (!result.IsSuccess || result.Data is null)
            {
                NewCustomerError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                return;
            }

            SelectCustomer(result.Data);
            _hasPendingQuickAddCustomerSuccess = true;
            FormInfo = Localization.Strings.Sales_CustomerCreatedPendingInvoiceSave;
            CloseCustomerQuickAdd();
        }
        finally
        {
            IsSavingCustomer = false;
        }
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
            if (ct.IsCancellationRequested) return;

            var url = $"/api/v1/customers?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
            var result = await ApiClient.GetAsync<PagedResponse<CustomerDto>>(url, ct);
            if (ct.IsCancellationRequested) return;

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
            results => HasCustomerQuickAddSuggestions = results,
            _customerSuggestionCts.Token);
    }

    private async Task DebounceSearchVariantsAsync(string query, CancellationToken ct)
    {
        try
        {
            IsVariantSearchLoading = true;
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;

            var url = $"/api/v1/variants?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
            var result = await ApiClient.GetAsync<PagedResponse<VariantListDto>>(url, ct);
            if (ct.IsCancellationRequested) return;

            VariantSearchResults.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var variant in result.Data.Items)
                    VariantSearchResults.Add(variant);
            }

            HasVariantSearchResults = VariantSearchResults.Count > 0;
            VariantSearchNote = HasVariantSearchResults
                ? string.Empty
                : result.IsSuccess
                    ? Localization.Strings.Variant_NoResults
                    : result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            IsVariantSearchLoading = false;
        }
    }

    private async Task HydrateLineVariantPricingAsync(IEnumerable<SalesLineVm> lines)
    {
        var lineMap = lines
            .Where(line => line.VariantId != Guid.Empty)
            .GroupBy(line => line.VariantId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var variantId in lineMap.Keys)
        {
            var result = await ApiClient.GetAsync<VariantListDto>($"/api/v1/variants/{variantId}");
            if (!result.IsSuccess || result.Data is null)
                continue;

            foreach (var line in lineMap[variantId])
            {
                line.RetailPrice = result.Data.RetailPrice;
                line.WholesalePrice = result.Data.WholesalePrice;
            }
        }
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

    private void ResetForm()
    {
        _editingId = null;
        FormInvoiceNumber = string.Empty;
        FormInvoiceDate = DateTime.UtcNow;
        FormNotes = string.Empty;
        ResetPendingQuickAddCustomerState();
        FormError = string.Empty;
        FormStatus = Localization.Strings.Status_Draft;
        SelectedCustomer = null;
        CustomerSearchText = string.Empty;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;
        ClearNotification();
        CloseCustomerQuickAdd();
        CloseVariantSearch();
        Lines.Clear();
        AddLine();
    }

    private void ClearNotification()
    {
        NotificationMessage = string.Empty;
        NotificationType = "Info";
    }

    private void ShowNotification(string message, string type)
    {
        if (NotificationMessage == message)
            NotificationMessage = string.Empty;
        NotificationMessage = message;
        NotificationType = type;
    }

    public void ClearTransientStatusForNavigation()
    {
        ClearNotification();

        if (!IsEditing)
            FormError = string.Empty;
    }

    private void ResetPendingQuickAddCustomerState()
    {
        _hasPendingQuickAddCustomerSuccess = false;
        FormInfo = string.Empty;
    }

    private string BuildSaveFailureMessage(string? errorMessage)
    {
        var detail = string.IsNullOrWhiteSpace(errorMessage)
            ? Localization.Strings.State_UnexpectedError
            : errorMessage;

        return _hasPendingQuickAddCustomerSuccess
            ? string.Format(Localization.Strings.Sales_CustomerCreatedInvoiceSaveFailed, detail)
            : string.Format(Localization.Strings.Sales_SaveFailed, detail);
    }

    private static DateTime? NormalizeInvoiceDateForRequest(DateTime? invoiceDate)
    {
        if (!invoiceDate.HasValue)
            return null;

        var value = invoiceDate.Value;
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.TimeOfDay == TimeSpan.Zero)
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private void ApplySalesWarehouseFilter(Guid? requiredWarehouseId = null)
    {
        var filtered = SalesWarehousePolicy.BuildSalesWarehouses(_activeWarehouses, requiredWarehouseId);

        Warehouses.Clear();
        foreach (var warehouse in filtered)
            Warehouses.Add(warehouse);
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SalesLineVm line in e.OldItems)
                line.PropertyChanged -= OnLinePropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (SalesLineVm line in e.NewItems)
                line.PropertyChanged += OnLinePropertyChanged;
        }

        OnPropertyChanged(nameof(InvoiceTotal));
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(InvoiceTotal));
    }

    private static string BuildVariantDisplay(string? productName, string sku, string? color = null, string? size = null)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!);
        if (!string.IsNullOrWhiteSpace(size)) parts.Add(size!);

        var meta = parts.Count > 0 ? $" ({string.Join(" / ", parts)})" : string.Empty;
        return $"{productName ?? Localization.Strings.Field_ProductName}{meta} — {sku}";
    }

    private string ResolveQuickAddSuggestionQuery()
    {
        var name = NewCustomerName.Trim();
        var phone = NewCustomerPhone.Trim();
        return phone.Length >= name.Length ? phone : name;
    }
}

public sealed partial class SalesLineVm : ObservableObject
{
    [ObservableProperty] private Guid _variantId;
    [ObservableProperty] private string _variantSku = string.Empty;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _variantDisplay = string.Empty;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal? _retailPrice;
    [ObservableProperty] private decimal? _wholesalePrice;

    public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;

    public string RetailPriceLabel => RetailPrice.HasValue
        ? string.Format(Localization.Strings.Sales_RetailPriceButton, InvoiceNumberFormat.Format(RetailPrice.Value))
        : string.Empty;

    public string WholesalePriceLabel => WholesalePrice.HasValue
        ? string.Format(Localization.Strings.Sales_WholesalePriceButton, InvoiceNumberFormat.Format(WholesalePrice.Value))
        : string.Empty;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));

    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));

    partial void OnDiscountAmountChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));

    partial void OnRetailPriceChanged(decimal? value) => OnPropertyChanged(nameof(RetailPriceLabel));

    partial void OnWholesalePriceChanged(decimal? value) => OnPropertyChanged(nameof(WholesalePriceLabel));
}
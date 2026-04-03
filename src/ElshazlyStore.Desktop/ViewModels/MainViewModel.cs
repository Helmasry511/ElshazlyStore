using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.ViewModels;

/// <summary>
/// ViewModel for the main shell window. Manages navigation, theme toggling,
/// current user display, permission-gated sidebar, and logout.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly ISessionService _sessionService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private string _activeNavItem = "Home";

    // ── User info ──
    [ObservableProperty]
    private string _currentUsername = string.Empty;

    [ObservableProperty]
    private bool _hasUser;

    // ── Permission visibility flags (sidebar sections) ──
    [ObservableProperty] private bool _canViewDashboard;

    // Commerce
    [ObservableProperty] private bool _canViewCommerce;
    [ObservableProperty] private bool _canViewProducts;
    [ObservableProperty] private bool _canViewCustomers;
    [ObservableProperty] private bool _canViewSuppliers;

    // Inventory
    [ObservableProperty] private bool _canViewInventory;
    [ObservableProperty] private bool _canViewWarehouses;
    [ObservableProperty] private bool _canViewStock;
    [ObservableProperty] private bool _canViewStockPost;
    [ObservableProperty] private bool _canViewPurchases;
    [ObservableProperty] private bool _canViewProduction;

    // Sales
    [ObservableProperty] private bool _canViewSales;
    [ObservableProperty] private bool _canViewSalesRead;
    [ObservableProperty] private bool _canViewSalesPos;
    [ObservableProperty] private bool _canViewSalesReturns;
    [ObservableProperty] private bool _canViewPurchaseReturns;

    // Accounting
    [ObservableProperty] private bool _canViewAccounting;
    [ObservableProperty] private bool _canViewBalances;
    [ObservableProperty] private bool _canViewPayments;

    // Admin
    [ObservableProperty] private bool _canViewAdmin;
    [ObservableProperty] private bool _canViewUsers;
    [ObservableProperty] private bool _canViewRoles;
    [ObservableProperty] private bool _canViewImport;
    [ObservableProperty] private bool _canViewReasonCodes;
    [ObservableProperty] private bool _canViewPrintConfig;

    /// <summary>Raised when the user logs out — App.xaml.cs listens to show LoginWindow.</summary>
    public event Action? LoggedOut;

    public MainViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        ISessionService sessionService,
        IPermissionService permissionService,
        ILogger<MainViewModel> logger)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _sessionService = sessionService;
        _permissionService = permissionService;
        _logger = logger;

        Title = Localization.Strings.AppTitle;
        _isDarkMode = _themeService.IsDarkMode;

        // Subscribe to navigation changes
        _navigationService.CurrentPageChanged += page => CurrentPage = page;

        // Subscribe to session events
        _sessionService.SessionEnded += OnSessionEnded;

        // Load user info and permissions
        RefreshUserState();

        // Navigate to Home by default
        _navigationService.NavigateTo<HomeViewModel>();
    }

    partial void OnIsDarkModeChanged(bool value) => _themeService.SetTheme(value);

    /// <summary>
    /// Refreshes all user-related properties from the current session.
    /// Called after login/restore and after permission changes.
    /// </summary>
    public void RefreshUserState()
    {
        var user = _sessionService.CurrentUser;
        HasUser = user is not null;
        CurrentUsername = user?.Username ?? string.Empty;

        // Individual permission checks
        CanViewDashboard = _permissionService.HasPermission(PermissionCodes.DashboardRead);

        // Commerce section
        CanViewProducts = _permissionService.HasPermission(PermissionCodes.ProductsRead);
        CanViewCustomers = _permissionService.HasPermission(PermissionCodes.CustomersRead);
        CanViewSuppliers = _permissionService.HasPermission(PermissionCodes.SuppliersRead);
        CanViewCommerce = CanViewProducts || CanViewCustomers || CanViewSuppliers;

        // Inventory section
        CanViewWarehouses = _permissionService.HasPermission(PermissionCodes.WarehousesRead);
        CanViewStock = _permissionService.HasPermission(PermissionCodes.StockRead);
        CanViewStockPost = _permissionService.HasPermission(PermissionCodes.StockPost);
        CanViewPurchases = _permissionService.HasPermission(PermissionCodes.PurchasesRead);
        CanViewProduction = _permissionService.HasPermission(PermissionCodes.ProductionRead);
        CanViewInventory = CanViewWarehouses || CanViewStock || CanViewPurchases || CanViewProduction;

        // Sales section
        CanViewSalesRead = _permissionService.HasPermission(PermissionCodes.SalesRead);
        CanViewSalesPos = _permissionService.HasAllPermissions(
            PermissionCodes.SalesRead,
            PermissionCodes.SalesWrite,
            PermissionCodes.SalesPost);
        CanViewSalesReturns = _permissionService.HasPermission(PermissionCodes.ViewSalesReturns);
        CanViewPurchaseReturns = _permissionService.HasPermission(PermissionCodes.ViewPurchaseReturns);
        CanViewSales = CanViewSalesRead || CanViewSalesPos || CanViewSalesReturns || CanViewPurchaseReturns;

        // Accounting section
        CanViewBalances = _permissionService.HasPermission(PermissionCodes.AccountingRead);
        CanViewPayments = _permissionService.HasPermission(PermissionCodes.PaymentsRead);
        CanViewAccounting = CanViewBalances || CanViewPayments;

        // Admin section
        CanViewUsers = _permissionService.HasPermission(PermissionCodes.UsersRead);
        CanViewRoles = _permissionService.HasPermission(PermissionCodes.RolesRead);
        CanViewImport = _permissionService.HasPermission(PermissionCodes.ImportMasterData);
        CanViewReasonCodes = _permissionService.HasPermission(PermissionCodes.ViewReasonCodes);
        CanViewPrintConfig = _permissionService.HasPermission(PermissionCodes.ManagePrintingPolicy);
        CanViewAdmin = CanViewUsers || CanViewRoles || CanViewImport || CanViewReasonCodes || CanViewPrintConfig;

        _logger.LogDebug("User state refreshed: HasUser={HasUser}, Username={Username}", HasUser, CurrentUsername);
    }

    [RelayCommand]
    private void NavigateTo(string pageName)
    {
        ActiveNavItem = pageName;

        switch (pageName)
        {
            case "Home":
                _navigationService.NavigateTo<HomeViewModel>();
                break;
            case "Settings":
                _navigationService.NavigateTo<SettingsViewModel>();
                break;
            case "Products":
                _navigationService.NavigateTo<ProductsViewModel>();
                break;
            case "Variants":
                _navigationService.NavigateTo<VariantsViewModel>();
                break;
            case "Customers":
                _navigationService.NavigateTo<CustomersViewModel>();
                break;
            case "Suppliers":
                _navigationService.NavigateTo<SuppliersViewModel>();
                break;
            case "Warehouses":
                _navigationService.NavigateTo<WarehousesViewModel>();
                break;
            case "StockBalances":
                _navigationService.NavigateTo<StockBalancesViewModel>();
                break;
            case "StockLedger":
                _navigationService.NavigateTo<StockLedgerViewModel>();
                break;
            case "StockMovements":
                _navigationService.NavigateTo<StockMovementsViewModel>();
                break;
            case "Purchases":
                _navigationService.NavigateTo<PurchasesViewModel>();
                break;
            case "Sales":
                _navigationService.NavigateTo<SalesViewModel>();
                break;
            case "SalesPos":
                _navigationService.NavigateTo<POSViewModel>();
                break;
            case "PurchaseReturns":
                _navigationService.NavigateTo<PurchaseReturnsViewModel>();
                break;
            case "SalesReturns":
                _navigationService.NavigateTo<SalesReturnsViewModel>();
                break;
            case "ReasonCodes":
                _navigationService.NavigateTo<ReasonCodesViewModel>();
                break;
            case "SupplierPayments":
                _navigationService.NavigateTo<SupplierPaymentsViewModel>();
                break;
            case "CustomerPayments":
                _navigationService.NavigateTo<CustomerPaymentsViewModel>();
                break;
            default:
                _logger.LogDebug("Navigation to '{Page}' — page not yet implemented", pageName);
                _navigationService.NavigateTo<HomeViewModel>();
                break;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _logger.LogInformation("User initiated logout");
        IsBusy = true;
        try
        {
            await _sessionService.LogoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
        }
        finally
        {
            IsBusy = false;
        }
        // SessionEnded event will fire → OnSessionEnded → LoggedOut
    }

    private void OnSessionEnded()
    {
        _logger.LogInformation("Session ended — raising LoggedOut event");
        HasUser = false;
        CurrentUsername = string.Empty;

        // Must dispatch to UI thread
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            LoggedOut?.Invoke();
        });
    }
}

using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using ElshazlyStore.Desktop.Localization;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;
using ElshazlyStore.Desktop.ViewModels;
using ElshazlyStore.Desktop.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ElshazlyStore.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public App()
    {
        // ── Arabic culture + RTL MUST be set before any XAML is parsed ──
        LocalizationBootstrapper.Initialize();
        InitializeComponent();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // ── Global exception handlers ──
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled dispatcher exception");
            Log.CloseAndFlush();
            MessageBox.Show(
                $"{Localization.Strings.State_UnexpectedError}\n{args.Exception.Message}",
                Localization.Strings.Dialog_ErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // prevent crash
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled AppDomain exception");
                Log.CloseAndFlush();
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        // ── Configuration ──
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        // ── Serilog ──
        var logPath = configuration["Logging:LogFilePath"] ?? "logs/elshazly-desktop-.log";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // ── DI Container ──
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        _serviceProvider = services.BuildServiceProvider();

        // ── Apply saved theme ──
        var themeService = _serviceProvider.GetRequiredService<IThemeService>();
        if (themeService is ThemeService ts)
        {
            ts.ApplyTheme(themeService.IsDarkMode);
        }

        Log.Information("ElshazlyStore Desktop started");

        // ── Try silent session restore ──
        var sessionService = _serviceProvider.GetRequiredService<ISessionService>();
        var restored = await sessionService.TryRestoreSessionAsync();

        if (restored)
        {
            Log.Information("Session restored — opening main window");
            ShowMainWindow();
        }
        else
        {
            Log.Information("No active session — showing login");
            ShowLoginWindow();
        }
    }

    private void ShowLoginWindow()
    {
        var loginWindow = _serviceProvider!.GetRequiredService<LoginWindow>();
        var loginVm = _serviceProvider!.GetRequiredService<LoginViewModel>();

        loginVm.LoginSucceeded += () =>
        {
            ShowMainWindow();   // must open MainWindow BEFORE closing login
            loginWindow.Close(); // now Windows.Count > 0, so OnClosed won't Shutdown
        };

        loginWindow.DataContext = loginVm;
        loginWindow.Show();
    }

    private void ShowMainWindow()
    {
        var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
        var mainVm = _serviceProvider!.GetRequiredService<MainViewModel>();

        mainVm.RefreshUserState();

        mainVm.LoggedOut += () =>
        {
            mainWindow.IsLoggingOut = true;
            mainWindow.Close();
            ShowLoginWindow();
        };

        mainWindow.DataContext = mainVm;
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddSingleton(configuration);

        // Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // User preferences (local file)
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();

        // Theme
        services.AddSingleton<IThemeService, ThemeService>();

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();

        // Messages
        services.AddSingleton<IMessageService, MessageService>();

        // Shared sales orchestration
        services.AddTransient<ISalesExecutionService, SalesExecutionService>();

        // Token store — DPAPI-backed for production
        services.AddSingleton<ITokenStore, SecureTokenStore>();

        // Session & Permission services
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IPermissionService, PermissionService>();

        // Stock change notifier (cross-VM signal for cache invalidation)
        services.AddSingleton<IStockChangeNotifier, StockChangeNotifier>();

        // HTTP handlers
        services.AddTransient<AuthHeaderHandler>();
        services.AddTransient<CorrelationIdHandler>();
        services.AddTransient<TokenRefreshHandler>();
        services.AddTransient<ApiTraceHandler>();

        // API client (typed HttpClient) — with auth + correlation handlers
        var apiBaseUrl = configuration["Api:BaseUrl"] ?? "https://localhost:5001";
        services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<CorrelationIdHandler>()
        .AddHttpMessageHandler<AuthHeaderHandler>()
        .AddHttpMessageHandler<TokenRefreshHandler>()
        .AddHttpMessageHandler<ApiTraceHandler>();

        // Auth-refresh HttpClient — NO auth handlers (used by TokenRefreshHandler to avoid loop)
        services.AddHttpClient("AuthRefresh", client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<VariantsViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<WarehousesViewModel>();
        services.AddTransient<StockBalancesViewModel>();
        services.AddTransient<StockLedgerViewModel>();
        services.AddTransient<StockMovementsViewModel>();
        services.AddTransient<PurchasesViewModel>();
        services.AddTransient<SalesViewModel>();
        services.AddTransient<POSViewModel>();
        services.AddTransient<PurchaseReturnsViewModel>();
        services.AddTransient<SalesReturnsViewModel>();
        services.AddTransient<ReasonCodesViewModel>();
        services.AddTransient<SupplierPaymentsViewModel>();
        services.AddTransient<CustomerPaymentsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("ElshazlyStore Desktop shutting down");
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

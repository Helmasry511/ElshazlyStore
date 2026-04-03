using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Persistence;
using ElshazlyStore.Infrastructure.Persistence.Interceptors;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElshazlyStore.Infrastructure;

/// <summary>
/// Registers Infrastructure services (DbContext, repositories, auth services) into DI.
/// Called from the API project's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var commandTimeout = configuration.GetValue<int>("Performance:CommandTimeoutSeconds", 30);

        // Audit interceptor (scoped because it depends on ICurrentUserService)
        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                npgsql.CommandTimeout(commandTimeout);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IPasswordHasher, AppPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

        // Import service
        services.AddScoped<ImportService>();

        // Stock service
        services.AddScoped<StockService>();

        // Accounting service (must be registered before services that depend on it)
        services.AddScoped<AccountingService>();

        // Purchase service
        services.AddScoped<PurchaseService>();

        // Production service
        services.AddScoped<ProductionService>();

        // Sales service
        services.AddScoped<SalesService>();

        // Dashboard service
        services.AddScoped<DashboardService>();

        // Printing policy service
        services.AddScoped<PrintingPolicyService>();

        // Reason code service
        services.AddScoped<ReasonCodeService>();

        // Sales return service
        services.AddScoped<SalesReturnService>();
        services.AddScoped<PurchaseReturnService>();

        // Disposition service
        services.AddScoped<DispositionService>();

        // Customer attachment filesystem storage
        services.AddSingleton<CustomerAttachmentStorageService>();

        return services;
    }
}

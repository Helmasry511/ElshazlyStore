using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElshazlyStore.Tests;

/// <summary>
/// Custom factory that replaces the Npgsql DbContext with SQLite in-memory
/// so integration tests run without Docker/PostgreSQL.
/// A single SQLite connection is kept open for the lifetime of the factory.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TestWebApplicationFactory()
    {
        // Open a shared in-memory SQLite connection that lives until Dispose.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:Secret",
            "test-secret-key-for-unit-tests-min-32-characters!!");
        builder.UseSetting("Jwt:Issuer", "ElshazlyStore");
        builder.UseSetting("Jwt:Audience", "ElshazlyStore.Desktop");
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenExpirationDays", "7");
        builder.UseSetting("ADMIN_DEFAULT_PASSWORD", "Admin@123!");

        // Provide a dummy connection string so DI registration doesn't throw
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Host=localhost;Database=unused");

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations
            var typesToRemove = new[]
            {
                typeof(DbContextOptions<AppDbContext>),
                typeof(DbContextOptions),
            };

            foreach (var type in typesToRemove)
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == type);
                if (descriptor is not null) services.Remove(descriptor);
            }

            // Remove the existing AppDbContext registration
            var dbCtxDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AppDbContext));
            if (dbCtxDescriptor is not null) services.Remove(dbCtxDescriptor);

            // Replace with SQLite in-memory
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlite(_connection);

                // Re-add the audit interceptor
                var interceptor = sp.GetRequiredService<ElshazlyStore.Infrastructure.Persistence.Interceptors.AuditInterceptor>();
                options.AddInterceptors(interceptor);
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

using System.Text;
using ElshazlyStore.Api.Authorization;
using ElshazlyStore.Api.Endpoints;
using ElshazlyStore.Api.Middleware;
using ElshazlyStore.Api.Services;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure;
using ElshazlyStore.Infrastructure.Seeding;
using ElshazlyStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// ── Bootstrap Serilog ──────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ElshazlyStore API");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

    // ── Infrastructure (EF Core + Npgsql + Auth services) ──────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── In-memory cache (barcode lookups, POS hot data) ────────
    builder.Services.AddMemoryCache();

    // ── Response compression (gzip for large grid/report payloads) ──
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<GzipCompressionProvider>();
    });

    // ── Request body size limits ───────────────────────────────
    var maxRequestBodyMB = builder.Configuration.GetValue<int>("RequestLimits:MaxRequestBodyMB", 10);
    var maxMultipartMB = builder.Configuration.GetValue<int>("RequestLimits:MaxMultipartMB", 10);
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = maxRequestBodyMB * 1024L * 1024;
    });
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = maxMultipartMB * 1024L * 1024;
    });

    // ── HTTP Context + Current User ────────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    // ── JWT Authentication ─────────────────────────────────────
    var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
    var jwtSecret = jwtSection["Secret"]
        ?? throw new InvalidOperationException("JWT Secret is not configured.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"] ?? "ElshazlyStore",
                ValidAudience = jwtSection["Audience"] ?? "ElshazlyStore.Desktop",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    // ── Authorization (permission-based) ───────────────────────
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
    builder.Services.AddAuthorization();

    // ── Swagger / OpenAPI ──────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "ElshazlyStore API — الشاذلي",
            Version = "v1",
            Description = "ERP/POS backend – server-owned source of truth"
        });
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter JWT token"
        });
        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // ── Database initialization ────────────────────────────────
    using (var dbScope = app.Services.CreateScope())
    {
        var db = dbScope.ServiceProvider.GetRequiredService<ElshazlyStore.Infrastructure.Persistence.AppDbContext>();
        if (app.Environment.IsEnvironment("Testing"))
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.MigrateAsync();
    }

    // ── Seed admin user/role/permissions ────────────────────────
    await AdminSeeder.SeedAsync(app.Services);

    // ── Middleware pipeline (order matters) ─────────────────────
    app.UseResponseCompression();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestTimingMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ElshazlyStore v1"));
    }

    // ── Endpoints ──────────────────────────────────────────────
    app.MapApiEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory integration tests
public partial class Program { }

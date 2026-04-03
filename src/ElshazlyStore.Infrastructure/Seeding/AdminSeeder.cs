using ElshazlyStore.Domain.Common;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using ElshazlyStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Infrastructure.Seeding;

/// <summary>
/// Seeds the Admin role, all permissions, and an initial Admin user at first run.
/// Password is read from configuration/env var ADMIN_DEFAULT_PASSWORD (default: Admin@123!).
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await SeedPermissionsAsync(db, logger);
        await SeedAdminRoleAsync(db, logger);
        await SeedAdminUserAsync(db, hasher, config, logger);
        await SeedDefaultWarehouseAsync(db, logger);
        await SeedSpecialWarehousesAsync(db, logger);
        await SeedDefaultReasonCodesAsync(db, logger);
    }

    private static async Task SeedPermissionsAsync(AppDbContext db, ILogger logger)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync();
        var toAdd = Permissions.All
            .Where(p => !existing.Contains(p.Code))
            .Select(p => new Permission
            {
                Id = Guid.NewGuid(),
                Code = p.Code,
                Description = p.Description,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Permissions.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} permissions", toAdd.Count);
        }
    }

    private static async Task SeedAdminRoleAsync(AppDbContext db, ILogger logger)
    {
        const string adminRoleName = "Admin";

        var adminRole = await db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Name == adminRoleName);

        if (adminRole is null)
        {
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = adminRoleName,
                Description = "Full system access",
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded Admin role");
        }

        // Ensure Admin role has ALL permissions
        var allPermissionIds = await db.Permissions.Select(p => p.Id).ToListAsync();
        var assignedIds = adminRole.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        var missing = allPermissionIds.Where(id => !assignedIds.Contains(id)).ToList();

        if (missing.Count > 0)
        {
            db.RolePermissions.AddRange(missing.Select(pid => new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = pid,
            }));
            await db.SaveChangesAsync();
            logger.LogInformation("Assigned {Count} missing permissions to Admin role", missing.Count);
        }
    }

    private static async Task SeedAdminUserAsync(
        AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger logger)
    {
        const string adminUsername = "admin";

        if (await db.Users.AnyAsync(u => u.Username == adminUsername))
            return;

        var password = config["ADMIN_DEFAULT_PASSWORD"] ?? "Admin@123!";
        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = adminUsername,
            PasswordHash = hasher.Hash(password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Admin user (username: {Username})", adminUsername);
    }

    private static async Task SeedDefaultWarehouseAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Warehouses.AnyAsync(w => w.IsDefault))
            return;

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = "MAIN",
            Name = "Main Warehouse",
            IsDefault = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default warehouse (code: {Code})", warehouse.Code);
    }

    /// <summary>
    /// Seeds special warehouses for inventory dispositions: QUARANTINE, SCRAP, REWORK.
    /// These are non-default, non-sellable warehouses used as destinations during disposition posting.
    /// </summary>
    private static async Task SeedSpecialWarehousesAsync(AppDbContext db, ILogger logger)
    {
        var specialCodes = new (string Code, string Name)[]
        {
            ("QUARANTINE", "Quarantine Warehouse"),
            ("SCRAP",      "Scrap Warehouse"),
            ("REWORK",     "Rework Warehouse"),
        };

        foreach (var (code, name) in specialCodes)
        {
            if (await db.Warehouses.AnyAsync(w => w.Code == code))
                continue;

            db.Warehouses.Add(new Warehouse
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                IsDefault = false,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        var added = db.ChangeTracker.Entries<Warehouse>()
            .Count(e => e.State == EntityState.Added);
        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} special warehouses (QUARANTINE, SCRAP, REWORK)", added);
        }
    }

    private static async Task SeedDefaultReasonCodesAsync(AppDbContext db, ILogger logger)
    {
        if (await db.ReasonCodes.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        // (Code, NameAr, Category, RequiresManagerApproval)
        var defaults = new (string Code, string NameAr, ReasonCategory Category, bool Approval)[]
        {
            // Disposition reasons
            ("DAMAGED",              "تالف",                     ReasonCategory.Disposition,    false),
            ("THEFT",                "سرقة",                     ReasonCategory.Disposition,    true),
            ("EXPIRED",              "منتهي الصلاحية",           ReasonCategory.Disposition,    false),
            ("NOT_MATCHING_SPEC",    "غير مطابق للمواصفات",      ReasonCategory.Disposition,    false),
            ("MANUFACTURING_DEFECT", "عيب تصنيع",               ReasonCategory.Disposition,    false),

            // Sales return reasons
            ("CUSTOMER_CHANGED_MIND","العميل غيّر رأيه",        ReasonCategory.SalesReturn,    false),
            ("WRONG_ITEM",           "صنف خاطئ",               ReasonCategory.SalesReturn,    false),
            ("DEFECTIVE_PRODUCT",    "منتج معيب",              ReasonCategory.SalesReturn,    false),

            // Purchase return reasons
            ("SUPPLIER_QUALITY",     "جودة غير مقبولة من المورد", ReasonCategory.PurchaseReturn, false),
            ("WRONG_DELIVERY",       "توريد خاطئ",              ReasonCategory.PurchaseReturn, false),
            ("OVER_DELIVERY",        "زيادة في التوريد",         ReasonCategory.PurchaseReturn, false),
        };

        foreach (var (code, nameAr, category, approval) in defaults)
        {
            db.ReasonCodes.Add(new ReasonCode
            {
                Id = Guid.NewGuid(),
                Code = code,
                NameAr = nameAr,
                Category = category,
                IsActive = true,
                RequiresManagerApproval = approval,
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} default reason codes", defaults.Length);
    }
}

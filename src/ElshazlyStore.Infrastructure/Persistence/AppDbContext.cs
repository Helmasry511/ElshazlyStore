using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElshazlyStore.Infrastructure.Persistence;

/// <summary>
/// Central EF Core DbContext for ElshazlyStore.
/// Entity configurations are applied from IEntityTypeConfiguration classes in this assembly.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SystemInfo> SystemInfo => Set<SystemInfo>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Phase 2 — Master Data
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<BarcodeReservation> BarcodeReservations => Set<BarcodeReservation>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    // Phase 3 — Inventory
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockMovementLine> StockMovementLines => Set<StockMovementLine>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    // Phase 4 — Procurement
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<PurchaseReceiptLine> PurchaseReceiptLines => Set<PurchaseReceiptLine>();
    public DbSet<SupplierPayable> SupplierPayables => Set<SupplierPayable>();

    // Phase 5 — Production
    public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();
    public DbSet<ProductionBatchLine> ProductionBatchLines => Set<ProductionBatchLine>();

    // Phase 6 — POS / Sales
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<CustomerReceivable> CustomerReceivables => Set<CustomerReceivable>();

    // Phase 7 — AR/AP Accounting & Payments
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Phase 9 — Printing Policy
    public DbSet<PrintProfile> PrintProfiles => Set<PrintProfile>();
    public DbSet<PrintRule> PrintRules => Set<PrintRule>();

    // Phase RET 0 — Reason Codes
    public DbSet<ReasonCode> ReasonCodes => Set<ReasonCode>();

    // Phase RET 1 — Sales Returns
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnLine> SalesReturnLines => Set<SalesReturnLine>();

    // Phase RET 2 — Purchase Returns
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnLine> PurchaseReturnLines => Set<PurchaseReturnLine>();

    // Phase RET 3 — Inventory Dispositions
    public DbSet<InventoryDisposition> InventoryDispositions => Set<InventoryDisposition>();
    public DbSet<InventoryDispositionLine> InventoryDispositionLines => Set<InventoryDispositionLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SystemInfo inline config (from Phase 0)
        modelBuilder.Entity<SystemInfo>(entity =>
        {
            entity.ToTable("system_info");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Value).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // Apply all IEntityTypeConfiguration<T> from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("sales_invoices");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.InvoiceNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Status).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.TotalAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.InvoiceDateUtc).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CashierUserId).IsRequired();

        builder.HasIndex(e => e.InvoiceNumber).IsUnique();
        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => e.CashierUserId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.InvoiceDateUtc);
        builder.HasIndex(e => e.CreatedAtUtc);

        // Phase 8 — Dashboard: composite index for posted-invoice date-range queries
        builder.HasIndex(e => new { e.Status, e.PostedAtUtc })
            .HasDatabaseName("IX_sales_invoices_status_posted");

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Cashier)
            .WithMany()
            .HasForeignKey(e => e.CashierUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.StockMovement)
            .WithMany()
            .HasForeignKey(e => e.StockMovementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

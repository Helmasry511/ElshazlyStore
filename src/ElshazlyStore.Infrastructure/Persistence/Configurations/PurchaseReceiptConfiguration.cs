using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("purchase_receipts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DocumentNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Status).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();

        builder.HasIndex(e => e.DocumentNumber).IsUnique();
        builder.HasIndex(e => e.SupplierId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc }).HasDatabaseName("IX_purchase_receipts_status_created");

        builder.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.StockMovement)
            .WithMany()
            .HasForeignKey(e => e.StockMovementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

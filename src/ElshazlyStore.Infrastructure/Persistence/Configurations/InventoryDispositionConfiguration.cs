using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class InventoryDispositionConfiguration : IEntityTypeConfiguration<InventoryDisposition>
{
    public void Configure(EntityTypeBuilder<InventoryDisposition> builder)
    {
        builder.ToTable("inventory_dispositions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DispositionNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Status).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DispositionDateUtc).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.RowVersion);

        builder.HasIndex(e => e.DispositionNumber).IsUnique();
        builder.HasIndex(e => e.WarehouseId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.DispositionDateUtc);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => new { e.Status, e.PostedAtUtc })
            .HasDatabaseName("IX_inventory_dispositions_status_posted");

        builder.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PostedBy)
            .WithMany()
            .HasForeignKey(e => e.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ApprovedBy)
            .WithMany()
            .HasForeignKey(e => e.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.StockMovement)
            .WithMany()
            .HasForeignKey(e => e.StockMovementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

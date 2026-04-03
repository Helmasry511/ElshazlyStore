using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class ProductionBatchConfiguration : IEntityTypeConfiguration<ProductionBatch>
{
    public void Configure(EntityTypeBuilder<ProductionBatch> builder)
    {
        builder.ToTable("production_batches");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Status).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();

        builder.HasIndex(e => e.BatchNumber).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc }).HasDatabaseName("IX_production_batches_status_created");

        builder.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ConsumeMovement)
            .WithMany()
            .HasForeignKey(e => e.ConsumeMovementId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ProduceMovement)
            .WithMany()
            .HasForeignKey(e => e.ProduceMovementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

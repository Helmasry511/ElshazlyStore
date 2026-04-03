using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class StockMovementLineConfiguration : IEntityTypeConfiguration<StockMovementLine>
{
    public void Configure(EntityTypeBuilder<StockMovementLine> builder)
    {
        builder.ToTable("stock_movement_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.QuantityDelta).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitCost).HasColumnType("numeric(18,4)");
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.HasIndex(e => e.VariantId);
        builder.HasIndex(e => e.WarehouseId);
        builder.HasIndex(e => new { e.VariantId, e.WarehouseId });

        builder.HasOne(e => e.StockMovement)
            .WithMany(m => m.Lines)
            .HasForeignKey(e => e.StockMovementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Variant)
            .WithMany()
            .HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

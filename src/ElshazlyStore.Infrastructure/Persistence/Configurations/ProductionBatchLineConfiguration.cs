using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class ProductionBatchLineConfiguration : IEntityTypeConfiguration<ProductionBatchLine>
{
    public void Configure(EntityTypeBuilder<ProductionBatchLine> builder)
    {
        builder.ToTable("production_batch_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.LineType).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitCost).HasColumnType("numeric(18,4)");

        builder.HasIndex(e => e.ProductionBatchId);
        builder.HasIndex(e => e.VariantId);

        builder.HasOne(e => e.Variant)
            .WithMany()
            .HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two collections on the parent: Inputs and Outputs are filtered from Lines by LineType.
        builder.HasOne(e => e.ProductionBatch)
            .WithMany(b => b.Lines)
            .HasForeignKey(e => e.ProductionBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

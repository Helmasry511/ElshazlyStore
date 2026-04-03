using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Sku).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Color).HasMaxLength(100);
        builder.Property(e => e.Size).HasMaxLength(50);
        builder.Property(e => e.RetailPrice).HasColumnType("numeric(18,2)");
        builder.Property(e => e.WholesalePrice).HasColumnType("numeric(18,2)");
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.LowStockThreshold).HasColumnType("numeric(18,4)");
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => e.Sku).IsUnique();
        builder.HasIndex(e => e.ProductId);

        builder.HasOne(e => e.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

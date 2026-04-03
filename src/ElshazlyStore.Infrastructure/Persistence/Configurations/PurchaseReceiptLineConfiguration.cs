using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("purchase_receipt_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitCost).HasColumnType("numeric(18,4)").IsRequired();

        builder.HasIndex(e => e.PurchaseReceiptId);
        builder.HasIndex(e => e.VariantId);

        builder.HasOne(e => e.PurchaseReceipt)
            .WithMany(r => r.Lines)
            .HasForeignKey(e => e.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Variant)
            .WithMany()
            .HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

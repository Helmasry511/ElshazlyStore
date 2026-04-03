using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class SupplierPayableConfiguration : IEntityTypeConfiguration<SupplierPayable>
{
    public void Configure(EntityTypeBuilder<SupplierPayable> builder)
    {
        builder.ToTable("supplier_payables");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.IsPaid).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => e.SupplierId);
        builder.HasIndex(e => e.PurchaseReceiptId).IsUnique();

        builder.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PurchaseReceipt)
            .WithMany()
            .HasForeignKey(e => e.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

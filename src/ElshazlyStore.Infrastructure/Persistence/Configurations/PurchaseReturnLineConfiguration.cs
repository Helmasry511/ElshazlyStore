using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReturnLineConfiguration : IEntityTypeConfiguration<PurchaseReturnLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnLine> builder)
    {
        builder.ToTable("purchase_return_lines");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitCost).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.LineTotal).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.DispositionType).IsRequired()
            .HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasIndex(e => e.PurchaseReturnId);
        builder.HasIndex(e => e.VariantId);
        builder.HasIndex(e => e.ReasonCodeId);

        builder.HasOne(e => e.PurchaseReturn)
            .WithMany(pr => pr.Lines)
            .HasForeignKey(e => e.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Variant)
            .WithMany()
            .HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReasonCode)
            .WithMany()
            .HasForeignKey(e => e.ReasonCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

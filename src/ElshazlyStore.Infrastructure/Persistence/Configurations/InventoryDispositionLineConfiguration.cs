using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class InventoryDispositionLineConfiguration : IEntityTypeConfiguration<InventoryDispositionLine>
{
    public void Configure(EntityTypeBuilder<InventoryDispositionLine> builder)
    {
        builder.ToTable("inventory_disposition_lines");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.DispositionType).IsRequired()
            .HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasIndex(e => e.InventoryDispositionId);
        builder.HasIndex(e => e.VariantId);
        builder.HasIndex(e => e.ReasonCodeId);

        builder.HasOne(e => e.InventoryDisposition)
            .WithMany(d => d.Lines)
            .HasForeignKey(e => e.InventoryDispositionId)
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

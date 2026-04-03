using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class BarcodeReservationConfiguration : IEntityTypeConfiguration<BarcodeReservation>
{
    public void Configure(EntityTypeBuilder<BarcodeReservation> builder)
    {
        builder.ToTable("barcode_reservations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Barcode).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ReservedAtUtc).IsRequired();
        builder.Property(e => e.Status).IsRequired()
            .HasConversion<string>().HasMaxLength(20);

        // Global unique barcode — never reused
        builder.HasIndex(e => e.Barcode).IsUnique();

        // One-to-one with ProductVariant
        builder.HasOne(e => e.Variant)
            .WithOne(v => v.BarcodeReservation)
            .HasForeignKey<BarcodeReservation>(e => e.VariantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PartyType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Method)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(e => e.WalletName).HasMaxLength(128);
        builder.Property(e => e.Reference).HasMaxLength(256);
        builder.Property(e => e.PaymentDateUtc).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.PartyType, e.PartyId });
        builder.HasIndex(e => e.PaymentDateUtc).HasDatabaseName("IX_payments_date");
        builder.HasIndex(e => e.Method).HasDatabaseName("IX_payments_method");

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

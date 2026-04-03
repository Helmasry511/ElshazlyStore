using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CustomerCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Phone2).HasMaxLength(30);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        // CP-3A: Credit profile fields
        builder.Property(e => e.WhatsApp).HasMaxLength(30);
        builder.Property(e => e.WalletNumber).HasMaxLength(50);
        builder.Property(e => e.InstaPayId).HasMaxLength(50);
        builder.Property(e => e.CommercialName).HasMaxLength(300);
        builder.Property(e => e.CommercialAddress).HasMaxLength(500);
        builder.Property(e => e.NationalIdNumber).HasMaxLength(20);

        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.CustomerCode).IsUnique();
        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.Phone);
    }
}

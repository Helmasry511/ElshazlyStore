using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class ReasonCodeConfiguration : IEntityTypeConfiguration<ReasonCode>
{
    public void Configure(EntityTypeBuilder<ReasonCode> builder)
    {
        builder.ToTable("reason_codes");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(100).IsRequired();
        builder.Property(e => e.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.RequiresManagerApproval).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.IsActive);
    }
}

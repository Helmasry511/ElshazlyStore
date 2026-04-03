using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class PrintProfileConfiguration : IEntityTypeConfiguration<PrintProfile>
{
    public void Configure(EntityTypeBuilder<PrintProfile> builder)
    {
        builder.ToTable("print_profiles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.IsDefault).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.IsDefault).HasDatabaseName("IX_print_profiles_is_default");
    }
}

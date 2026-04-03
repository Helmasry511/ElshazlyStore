using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class PrintRuleConfiguration : IEntityTypeConfiguration<PrintRule>
{
    public void Configure(EntityTypeBuilder<PrintRule> builder)
    {
        builder.ToTable("print_rules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ScreenCode).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ConfigJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.Enabled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        // Each profile can have at most one rule per screen/report
        builder.HasIndex(e => new { e.PrintProfileId, e.ScreenCode })
            .IsUnique()
            .HasDatabaseName("IX_print_rules_profile_screen");

        builder.HasIndex(e => e.ScreenCode).HasDatabaseName("IX_print_rules_screen_code");

        builder.HasOne(e => e.Profile)
            .WithMany(p => p.Rules)
            .HasForeignKey(e => e.PrintProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

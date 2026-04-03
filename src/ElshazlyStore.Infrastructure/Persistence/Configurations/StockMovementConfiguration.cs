using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).IsRequired()
            .HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Reference).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.PostedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();

        builder.HasIndex(e => e.PostedAtUtc);
        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.Reference).HasDatabaseName("IX_stock_movements_reference");
        builder.HasIndex(e => new { e.Type, e.PostedAtUtc }).HasDatabaseName("IX_stock_movements_type_posted");

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

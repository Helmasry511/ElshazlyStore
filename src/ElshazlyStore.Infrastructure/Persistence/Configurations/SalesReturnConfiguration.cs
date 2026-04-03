using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("sales_returns");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ReturnNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Status).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.TotalAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.ReturnDateUtc).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();

        builder.HasIndex(e => e.ReturnNumber).IsUnique();
        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => e.OriginalSalesInvoiceId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ReturnDateUtc);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => new { e.Status, e.PostedAtUtc })
            .HasDatabaseName("IX_sales_returns_status_posted");

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.OriginalSalesInvoice)
            .WithMany()
            .HasForeignKey(e => e.OriginalSalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PostedBy)
            .WithMany()
            .HasForeignKey(e => e.PostedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.StockMovement)
            .WithMany()
            .HasForeignKey(e => e.StockMovementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

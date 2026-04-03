using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
    {
        builder.ToTable("sales_invoice_lines");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.DiscountAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.LineTotal).HasColumnType("numeric(18,4)").IsRequired();

        builder.HasIndex(e => e.SalesInvoiceId);
        builder.HasIndex(e => e.VariantId);

        builder.HasOne(e => e.SalesInvoice)
            .WithMany(si => si.Lines)
            .HasForeignKey(e => e.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Variant)
            .WithMany()
            .HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

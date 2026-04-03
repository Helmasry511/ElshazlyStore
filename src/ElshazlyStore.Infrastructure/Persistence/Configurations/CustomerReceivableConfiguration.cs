using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class CustomerReceivableConfiguration : IEntityTypeConfiguration<CustomerReceivable>
{
    public void Configure(EntityTypeBuilder<CustomerReceivable> builder)
    {
        builder.ToTable("customer_receivables");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.IsPaid).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => e.SalesInvoiceId).IsUnique();

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SalesInvoice)
            .WithMany()
            .HasForeignKey(e => e.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class CustomerAttachmentConfiguration : IEntityTypeConfiguration<CustomerAttachment>
{
    public void Configure(EntityTypeBuilder<CustomerAttachment> builder)
    {
        builder.ToTable("customer_attachments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.StoredFileName).HasMaxLength(255);
        builder.Property(e => e.RelativePath).HasMaxLength(500);
        builder.Property(e => e.CustomerCode).HasMaxLength(20);
        builder.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FileSize).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(30).IsRequired().HasDefaultValue("other");
        builder.Property(e => e.FileContent);  // nullable for new filesystem-stored attachments
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CustomerId);
    }
}

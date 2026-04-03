using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(500).IsRequired();
        builder.Property(e => e.FileHash).HasMaxLength(128).IsRequired();
        builder.Property(e => e.UploadedByUserId).IsRequired();
        builder.Property(e => e.Status).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.PreviewResultJson).HasColumnType("jsonb");
        builder.Property(e => e.ErrorSummary).HasMaxLength(4000);
        builder.Property(e => e.FileContent).IsRequired();

        builder.HasIndex(e => e.FileHash);
        builder.HasIndex(e => e.Status);
    }
}

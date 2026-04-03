using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TimestampUtc).IsRequired();
        builder.Property(e => e.Username).HasMaxLength(100);
        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.Action).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Module).HasMaxLength(100);
        builder.Property(e => e.EntityName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PrimaryKey).HasMaxLength(200);
        builder.Property(e => e.OldValues).HasColumnType("jsonb");
        builder.Property(e => e.NewValues).HasColumnType("jsonb");

        builder.HasIndex(e => e.TimestampUtc);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.EntityName);
    }
}

using ElshazlyStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElshazlyStore.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PartyType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.EntryType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(e => e.Reference).HasMaxLength(256);
        builder.Property(e => e.Notes).HasMaxLength(1024);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.PartyType, e.PartyId });
        builder.HasIndex(e => e.RelatedInvoiceId);
        builder.HasIndex(e => e.RelatedPaymentId);
        builder.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_ledger_entries_created");
        builder.HasIndex(e => e.EntryType).HasDatabaseName("IX_ledger_entries_entry_type");

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

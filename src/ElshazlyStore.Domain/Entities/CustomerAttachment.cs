namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// File attachment linked to a customer (national ID, contract, etc.).
/// New attachments are stored on the filesystem; legacy attachments may still have FileContent (bytea).
/// </summary>
public sealed class CustomerAttachment
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Original file name as uploaded (for display).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Safe file name used on disk (collision-free, no invalid chars).</summary>
    public string? StoredFileName { get; set; }

    /// <summary>Relative path from the attachments root to the stored file.</summary>
    public string? RelativePath { get; set; }

    /// <summary>Denormalized customer code for filesystem folder lookup.</summary>
    public string? CustomerCode { get; set; }

    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Category { get; set; } = "other"; // national_id, contract, other

    /// <summary>
    /// Legacy: full file content stored as bytea. Null for new filesystem-stored attachments.
    /// </summary>
    public byte[]? FileContent { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}

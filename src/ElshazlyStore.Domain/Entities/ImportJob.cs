namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Tracks import jobs for master data (products, customers, suppliers).
/// Supports a two-step preview → commit workflow.
/// </summary>
public sealed class ImportJob
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;           // "Products", "Customers", "Suppliers"
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;       // SHA-256 of uploaded file
    public Guid UploadedByUserId { get; set; }
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Previewed;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CommittedAtUtc { get; set; }

    /// <summary>Preview result serialized as JSON (row counts, errors).</summary>
    public string? PreviewResultJson { get; set; }

    /// <summary>Error summary on commit failure.</summary>
    public string? ErrorSummary { get; set; }

    /// <summary>Raw file bytes stored for commit step.</summary>
    public byte[] FileContent { get; set; } = [];
}

/// <summary>
/// Import job lifecycle status.
/// </summary>
public enum ImportJobStatus
{
    Previewed = 0,
    Committed = 1,
    Failed = 2
}

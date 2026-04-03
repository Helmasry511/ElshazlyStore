namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Immutable audit log entry. Captures who changed what, when.
/// OldValues/NewValues stored as JSONB (size-limited at infra layer).
/// </summary>
public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    // Who
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }

    // What
    public string Action { get; set; } = string.Empty;   // INSERT / UPDATE / DELETE
    public string? Module { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string? PrimaryKey { get; set; }

    // Changes
    public string? OldValues { get; set; }  // JSONB
    public string? NewValues { get; set; }  // JSONB
}

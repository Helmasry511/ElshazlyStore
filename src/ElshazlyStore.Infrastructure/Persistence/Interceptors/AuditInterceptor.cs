using System.Text.Json;
using ElshazlyStore.Domain.Entities;
using ElshazlyStore.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ElshazlyStore.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that writes AuditLog rows for every
/// Insert/Update/Delete on business entities. Excludes AuditLog and RefreshToken.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    // Entities excluded from audit logging
    private static readonly HashSet<string> ExcludedEntities =
    [
        nameof(AuditLog),
        nameof(RefreshToken),
    ];

    // Properties redacted from audit OldValues/NewValues (never serialised).
    // FileContent is a binary blob — serialising it to base64 inside JSON then
    // truncating at MaxJsonLength produces invalid JSONB, causing a PostgreSQL
    // "invalid input syntax for type json" 500 on every attachment upload.
    private static readonly HashSet<string> RedactedFields =
    [
        "PasswordHash",
        "Token",
        "TokenHash",
        "ReplacedByToken",
        "ReplacedByTokenHash",
        "RefreshToken",
        "Secret",
        "FileContent",  // binary attachment data — excluded to prevent invalid-jsonb truncation
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 3
    };

    // Max JSON size per field (4 KB)
    private const int MaxJsonLength = 4096;

    private List<AuditEntry>? _pendingAudits;

    public AuditInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        _pendingAudits = CaptureChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && _pendingAudits is { Count: > 0 })
        {
            await WriteAuditLogsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditEntry> CaptureChanges(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var entries = new List<AuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            var entityName = entry.Entity.GetType().Name;
            if (ExcludedEntities.Contains(entityName)) continue;
            if (entry.State is EntityState.Detached or EntityState.Unchanged) continue;

            var audit = new AuditEntry
            {
                EntityName = entityName,
                Action = entry.State switch
                {
                    EntityState.Added => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => "UNKNOWN"
                }
            };

            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.IsPrimaryKey())
                {
                    audit.PrimaryKeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    continue;
                }

                // Never serialize sensitive fields into audit logs
                if (RedactedFields.Contains(prop.Metadata.Name))
                    continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        audit.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        audit.OldValues[prop.Metadata.Name] = prop.OriginalValue;
                        break;
                    case EntityState.Modified when prop.IsModified:
                        audit.OldValues[prop.Metadata.Name] = prop.OriginalValue;
                        audit.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                        break;
                }
            }

            entries.Add(audit);
        }

        return entries;
    }

    private async Task WriteAuditLogsAsync(DbContext context, CancellationToken ct)
    {
        var logs = _pendingAudits!.Select(a => new AuditLog
        {
            TimestampUtc = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            IpAddress = _currentUser.IpAddress,
            UserAgent = Truncate(_currentUser.UserAgent, 512),
            CorrelationId = _currentUser.CorrelationId,
            Action = a.Action,
            EntityName = a.EntityName,
            PrimaryKey = Truncate(Serialize(a.PrimaryKeyValues), 200),
            OldValues = Truncate(Serialize(a.OldValues), MaxJsonLength),
            NewValues = Truncate(Serialize(a.NewValues), MaxJsonLength),
        });

        context.Set<AuditLog>().AddRange(logs);
        _pendingAudits = null;

        // Direct save to bypass this interceptor for audit rows
        await context.SaveChangesAsync(ct);
    }

    private static string? Serialize(Dictionary<string, object?> dict)
        => dict.Count == 0 ? null : JsonSerializer.Serialize(dict, JsonOptions);

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>Temporary holder for change tracking data before flush.</summary>
    private sealed class AuditEntry
    {
        public string EntityName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, object?> PrimaryKeyValues { get; } = [];
        public Dictionary<string, object?> OldValues { get; } = [];
        public Dictionary<string, object?> NewValues { get; } = [];
    }
}

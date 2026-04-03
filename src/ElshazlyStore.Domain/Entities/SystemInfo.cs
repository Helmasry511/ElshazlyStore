namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Placeholder entity to bootstrap EF Core migrations.
/// Stores basic system/deployment metadata.
/// </summary>
public sealed class SystemInfo
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

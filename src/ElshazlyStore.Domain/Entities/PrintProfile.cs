namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// A named printing profile that groups a set of print rules.
/// One profile can be marked as default for the organization.
/// </summary>
public sealed class PrintProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }

    // Navigation
    public ICollection<PrintRule> Rules { get; set; } = new List<PrintRule>();
}

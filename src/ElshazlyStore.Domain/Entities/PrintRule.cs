namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// A printing rule for a specific screen/report within a PrintProfile.
/// Contains the template/config as JSON for the client to interpret,
/// and an enabled flag to toggle rules without deleting them.
/// </summary>
public sealed class PrintRule
{
    public Guid Id { get; set; }
    public Guid PrintProfileId { get; set; }

    /// <summary>Identifies the screen or report this rule targets (e.g., "SALES_INVOICE", "PURCHASE_RECEIPT").</summary>
    public string ScreenCode { get; set; } = string.Empty;

    /// <summary>JSON blob defining printer preferences, field visibility, margins, etc.</summary>
    public string ConfigJson { get; set; } = "{}";

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }

    // Navigation
    public PrintProfile Profile { get; set; } = null!;
}

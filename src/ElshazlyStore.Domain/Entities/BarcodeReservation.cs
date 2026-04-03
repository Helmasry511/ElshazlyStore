namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Global barcode reservation. Barcodes are unique system-wide and never reused.
/// Status flow: Reserved → Assigned → Retired (terminal).
/// </summary>
public sealed class BarcodeReservation
{
    public Guid Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public DateTime ReservedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? VariantId { get; set; }
    public BarcodeStatus Status { get; set; } = BarcodeStatus.Reserved;

    // Navigation
    public ProductVariant? Variant { get; set; }
}

/// <summary>
/// Barcode lifecycle status. Once Retired a barcode can never be reused.
/// </summary>
public enum BarcodeStatus
{
    Reserved = 0,
    Assigned = 1,
    Retired = 2
}

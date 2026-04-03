namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Customer record. Code is unique and server-generated if not provided.
/// CustomerCode is the immutable global identifier (YYYY-NNNNNN) used for filesystem folder naming.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Immutable global customer code. Format: YYYY-NNNNNN (digits and "-" only).
    /// Used as the filesystem folder name for customer attachments.
    /// Never changes after creation. Never reused.
    /// </summary>
    public string CustomerCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Notes { get; set; }

    // CP-3A: Credit profile fields
    public string? WhatsApp { get; set; }
    public string? WalletNumber { get; set; }
    public string? InstaPayId { get; set; }
    public string? CommercialName { get; set; }
    public string? CommercialAddress { get; set; }
    public string? NationalIdNumber { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

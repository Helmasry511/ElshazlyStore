namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Payment record for customer or supplier.
/// Creates a corresponding negative ledger entry to reduce outstanding balance.
/// </summary>
public sealed class Payment
{
    public Guid Id { get; set; }
    public PartyType PartyType { get; set; }
    public Guid PartyId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    /// <summary>Required when Method == EWallet. The wallet name entered by user.</summary>
    public string? WalletName { get; set; }
    public string? Reference { get; set; }
    public DateTime PaymentDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }

    // Navigation
    public User CreatedBy { get; set; } = null!;
}

public enum PaymentMethod
{
    Cash = 0,
    InstaPay = 1,
    EWallet = 2,
    Visa = 3,
}

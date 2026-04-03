namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Unified AR/AP ledger entry. Balances are derived by summing entries,
/// never stored as a mutable field.
/// PartyType discriminates Customer (AR) vs Supplier (AP).
/// Positive Amount = party owes us (receivable) or we owe party (payable).
/// Negative Amount = reduces outstanding (e.g. payment received/made).
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; set; }
    public PartyType PartyType { get; set; }
    public Guid PartyId { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public Guid? RelatedPaymentId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }

    // Navigation
    public User CreatedBy { get; set; } = null!;
}

public enum PartyType
{
    Customer = 0,
    Supplier = 1,
}

public enum LedgerEntryType
{
    /// <summary>Opening balance imported or manually entered.</summary>
    OpeningBalance = 0,
    /// <summary>Invoice posted — increases outstanding.</summary>
    Invoice = 1,
    /// <summary>Payment received/made — decreases outstanding.</summary>
    Payment = 2,
    /// <summary>Credit note from sales return — decreases outstanding (negative amount).</summary>
    CreditNote = 3,
    /// <summary>Debit note from purchase return — reduces supplier payable (negative amount).</summary>
    DebitNote = 4,
}

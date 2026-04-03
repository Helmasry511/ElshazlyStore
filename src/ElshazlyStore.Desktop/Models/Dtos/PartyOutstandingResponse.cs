namespace ElshazlyStore.Desktop.Models.Dtos;

/// <summary>
/// Deserialization target for GET /api/v1/accounting/balances/{partyType}/{partyId}.
/// Returns the computed outstanding balance for a single party.
/// </summary>
public sealed class PartyOutstandingResponse
{
    public Guid PartyId { get; set; }
    public string? PartyType { get; set; }

    /// <summary>
    /// The outstanding amount owed by (Customer) or to (Supplier) this party.
    /// Derived from ledger entries — always the server's computed truth.
    /// </summary>
    public decimal Outstanding { get; set; }
}

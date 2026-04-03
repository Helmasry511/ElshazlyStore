namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Categorises reason codes by usage context.
/// </summary>
public enum ReasonCategory
{
    /// <summary>Applies to any context.</summary>
    General = 0,

    /// <summary>Customer-initiated sales return.</summary>
    SalesReturn = 1,

    /// <summary>Supplier-directed purchase return.</summary>
    PurchaseReturn = 2,

    /// <summary>Pre-sale dispositions: damage, theft, expiry, etc.</summary>
    Disposition = 3,
}

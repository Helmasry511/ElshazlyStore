namespace ElshazlyStore.Domain.Entities;

/// <summary>
/// Strict disposition types for damaged/defective/returned stock.
/// Stored as string in DB to maintain readability.
/// </summary>
public enum DispositionType
{
    /// <summary>هالك/تالف نهائي — irreparable, to be destroyed.</summary>
    Scrap = 0,

    /// <summary>قابل لإعادة التشغيل — can be reworked/repaired.</summary>
    Rework = 1,

    /// <summary>مرتجع للمورد — return to supplier/vendor.</summary>
    ReturnToVendor = 2,

    /// <summary>يرجع مخزن صالح للبيع — return to sellable stock.</summary>
    ReturnToStock = 3,

    /// <summary>حجز/عزل للفحص — quarantine for inspection.</summary>
    Quarantine = 4,

    /// <summary>تسوية/خصم كمي (سرقة/فقد) — write-off for theft/loss.</summary>
    WriteOff = 5,
}

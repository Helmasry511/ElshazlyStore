using System.Globalization;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Smart invoice number formatter: shows decimals only when a real fractional part exists.
/// 4000 → "4,000", 4780 → "4,780", 19.50 → "19.50", 0 → "0"
/// </summary>
public static class InvoiceNumberFormat
{
    /// <summary>
    /// Format a decimal for display: integer values get no decimal suffix,
    /// fractional values get exactly 2 decimal places.
    /// Thousands separator is always used.
    /// </summary>
    public static string Format(decimal value)
    {
        // Check if the value has a real fractional part
        if (value == Math.Truncate(value))
            return value.ToString("N0", CultureInfo.InvariantCulture);

        return value.ToString("N2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Format a nullable decimal. Returns "—" if null.
    /// </summary>
    public static string FormatOrDash(decimal? value)
    {
        return value.HasValue ? Format(value.Value) : "—";
    }
}

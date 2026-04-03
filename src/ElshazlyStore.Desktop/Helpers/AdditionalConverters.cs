using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ElshazlyStore.Desktop.Localization;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Converts a bool (IsActive) to Arabic status text: نشط / غير نشط.
/// </summary>
public sealed class BoolToActiveStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Strings.Status_Active : Strings.Status_Inactive;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a non-null/non-empty string to Visible, else Collapsed.
/// </summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? false : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? false : true;
}

/// <summary>
/// Formats a decimal using the smart InvoiceNumberFormat rule:
/// integers → no decimals (4000 → "4,000"), fractional → 2 decimals (19.50 → "19.50").
/// </summary>
public sealed class SmartNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return InvoiceNumberFormat.Format(d);
        if (value is double dbl)
            return InvoiceNumberFormat.Format((decimal)dbl);
        if (value is int i)
            return InvoiceNumberFormat.Format(i);
        return value?.ToString() ?? "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

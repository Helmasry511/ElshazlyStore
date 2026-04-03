using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Returns Visibility.Visible when string values match, Collapsed otherwise.
/// Used to highlight the active navigation item.
/// </summary>
public sealed class StringEqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && parameter is string target)
            return str == target ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

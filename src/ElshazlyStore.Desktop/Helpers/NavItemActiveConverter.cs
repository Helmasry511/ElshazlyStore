using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Compares CommandParameter value with a bound string to toggle nav item styling.
/// Returns the ActiveNavButtonStyle when matched, NavButtonStyle otherwise.
/// </summary>
public sealed class NavItemActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 &&
            values[0] is string current &&
            values[1] is string itemName &&
            current == itemName)
        {
            return Application.Current.FindResource("ActiveNavButtonStyle");
        }
        return Application.Current.FindResource("NavButtonStyle");
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

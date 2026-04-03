using System.Windows;
using ElshazlyStore.Desktop.Localization;

namespace ElshazlyStore.Desktop.Services;

public sealed class MessageService : IMessageService
{
    private static string DefaultTitle => Strings.AppName;

    public void ShowInfo(string message, string? title = null)
    {
        MessageBox.Show(message, title ?? DefaultTitle, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string? title = null)
    {
        MessageBox.Show(message, title ?? DefaultTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string? title = null)
    {
        MessageBox.Show(message, title ?? DefaultTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirm(string message, string? title = null)
    {
        var result = MessageBox.Show(message, title ?? DefaultTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}

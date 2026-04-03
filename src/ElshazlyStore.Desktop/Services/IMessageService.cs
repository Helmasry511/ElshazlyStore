namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Displays messages/notifications to the user in a standardized way.
/// </summary>
public interface IMessageService
{
    void ShowInfo(string message, string? title = null);
    void ShowWarning(string message, string? title = null);
    void ShowError(string message, string? title = null);
    bool ShowConfirm(string message, string? title = null);
}

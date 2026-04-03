using System.Windows;

namespace ElshazlyStore.Desktop.Views;

public partial class MainWindow : Window
{
    /// <summary>
    /// When true, the window is closing due to logout (not user X button).
    /// Prevents app shutdown so the login window can reappear.
    /// </summary>
    public bool IsLoggingOut { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // If user closes main window via X button (not logout), shut down the app
        if (!IsLoggingOut)
        {
            Application.Current.Shutdown();
        }
    }
}

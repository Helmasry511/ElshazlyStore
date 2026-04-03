using System.Windows;
using System.Windows.Input;

namespace ElshazlyStore.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Focus the username box on load
        UsernameBox.Focus();
        Keyboard.Focus(UsernameBox);

        // PasswordBox.Password is not a DependencyProperty, so XAML binding
        // never updates.  Push the value into CommandParameter manually.
        PasswordBox.PasswordChanged += (_, _) =>
        {
            LoginButton.CommandParameter = PasswordBox.Password;
        };
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // If user closes the login window via X button and no main window is open, shut down
        if (Application.Current.Windows.Count == 0)
        {
            Application.Current.Shutdown();
        }
    }
}

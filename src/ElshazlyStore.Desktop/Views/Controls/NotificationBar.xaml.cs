using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ElshazlyStore.Desktop.Views.Controls;

public partial class NotificationBar : UserControl
{
    private DispatcherTimer? _autoDismissTimer;

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(NotificationBar),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    public static readonly DependencyProperty NotificationTypeProperty =
        DependencyProperty.Register(nameof(NotificationType), typeof(string), typeof(NotificationBar),
            new PropertyMetadata("Info", OnTypeChanged));

    public static readonly DependencyProperty AutoDismissSecondsProperty =
        DependencyProperty.Register(nameof(AutoDismissSeconds), typeof(int), typeof(NotificationBar),
            new PropertyMetadata(5));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string NotificationType
    {
        get => (string)GetValue(NotificationTypeProperty);
        set => SetValue(NotificationTypeProperty, value);
    }

    public int AutoDismissSeconds
    {
        get => (int)GetValue(AutoDismissSecondsProperty);
        set => SetValue(AutoDismissSecondsProperty, value);
    }

    public NotificationBar()
    {
        InitializeComponent();
    }

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NotificationBar bar)
            bar.UpdateVisual();
    }

    private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NotificationBar bar)
            bar.UpdateVisual();
    }

    public void Show(string message, string type = "Info")
    {
        Message = message;
        NotificationType = type;
    }

    private void UpdateVisual()
    {
        // Always stop any previous timer first to avoid stale dismiss
        if (_autoDismissTimer is not null)
        {
            _autoDismissTimer.Stop();
            _autoDismissTimer.Tick -= OnAutoDismissTick;
            _autoDismissTimer = null;
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            NotifBorder.Visibility = Visibility.Collapsed;
            return;
        }

        MessageText.Text = Message;

        switch (NotificationType)
        {
            case "Success":
                NotifBorder.Background = (Brush)Resources["SuccessBgBrush"];
                NotifBorder.BorderBrush = (Brush)Resources["SuccessBorderBrush"];
                NotifBorder.BorderThickness = new Thickness(1);
                MessageText.Foreground = (Brush)Resources["SuccessTextBrush"];
                CopyButton.Visibility = Visibility.Collapsed;
                break;
            case "Warning":
                NotifBorder.Background = (Brush)Resources["WarningBgBrush"];
                NotifBorder.BorderBrush = (Brush)Resources["WarningBorderBrush"];
                NotifBorder.BorderThickness = new Thickness(1);
                MessageText.Foreground = (Brush)Resources["WarningTextBrush"];
                CopyButton.Visibility = Visibility.Collapsed;
                break;
            case "Error":
                NotifBorder.Background = (Brush)Resources["ErrorBgBrush"];
                NotifBorder.BorderBrush = (Brush)Resources["ErrorBorderBrushLocal"];
                NotifBorder.BorderThickness = new Thickness(1);
                MessageText.Foreground = (Brush)Resources["ErrorTextBrush"];
                CopyButton.Visibility = Visibility.Visible;
                break;
            default: // Info
                NotifBorder.Background = (Brush)Resources["InfoBgBrush"];
                NotifBorder.BorderBrush = (Brush)Resources["InfoBorderBrush"];
                NotifBorder.BorderThickness = new Thickness(1);
                MessageText.Foreground = (Brush)Resources["InfoTextBrush"];
                CopyButton.Visibility = Visibility.Collapsed;
                break;
        }

        NotifBorder.Visibility = Visibility.Visible;

        // Auto-dismiss for non-error types
        if (NotificationType != "Error" && AutoDismissSeconds > 0)
        {
            _autoDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AutoDismissSeconds)
            };
            _autoDismissTimer.Tick += OnAutoDismissTick;
            _autoDismissTimer.Start();
        }
    }

    private void OnAutoDismissTick(object? sender, EventArgs e)
    {
        if (_autoDismissTimer is not null)
        {
            _autoDismissTimer.Stop();
            _autoDismissTimer.Tick -= OnAutoDismissTick;
            _autoDismissTimer = null;
        }
        Dismiss();
    }

    private void Dismiss()
    {
        if (_autoDismissTimer is not null)
        {
            _autoDismissTimer.Stop();
            _autoDismissTimer.Tick -= OnAutoDismissTick;
            _autoDismissTimer = null;
        }
        NotifBorder.Visibility = Visibility.Collapsed;
        // Use SetCurrentValue instead of direct assignment to preserve data binding.
        // Direct assignment (Message = ...) breaks OneWay bindings, preventing
        // subsequent ViewModel updates from reaching the control.
        SetCurrentValue(MessageProperty, string.Empty);
    }

    private void OnDismissClick(object sender, RoutedEventArgs e) => Dismiss();

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Message); }
        catch { /* clipboard locked */ }
    }
}

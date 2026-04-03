using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ElshazlyStore.Desktop.Views.Controls;

public partial class ErrorDisplay : UserControl
{
    public ErrorDisplay()
    {
        InitializeComponent();
        CopyButton.Click += OnCopyClick;
    }

    // ── Message ──
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ErrorDisplay),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErrorDisplay ctrl)
            ctrl.ErrorMessageText.Text = e.NewValue as string ?? string.Empty;
    }

    // ── RetryCommand ──
    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(nameof(RetryCommand), typeof(ICommand), typeof(ErrorDisplay),
            new PropertyMetadata(null, OnRetryCommandChanged));

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    private static void OnRetryCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErrorDisplay ctrl && e.NewValue is ICommand cmd)
            ctrl.RetryButton.Command = cmd;
    }

    // ── Copy ──
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var text = Message;
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }
}

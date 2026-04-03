using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ElshazlyStore.Desktop.ViewModels;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class POSPage : UserControl
{
    public POSPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not POSViewModel vm)
            return;

        vm.BarcodeFocusRequested += OnBarcodeFocusRequested;
        vm.ClearTransientStatusForNavigation();
        await vm.InitializeCommand.ExecuteAsync(null);
        FocusBarcodeInput();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is POSViewModel vm)
            vm.BarcodeFocusRequested -= OnBarcodeFocusRequested;
    }

    private void OnBarcodeFocusRequested()
    {
        FocusBarcodeInput();
    }

    private void FocusBarcodeInput()
    {
        if (DataContext is POSViewModel { IsCustomerQuickAddOpen: true })
            return;

        Dispatcher.BeginInvoke(() =>
        {
            BarcodeInputBox.Focus();
            Keyboard.Focus(BarcodeInputBox);
            BarcodeInputBox.SelectAll();
        }, DispatcherPriority.Background);
    }
}

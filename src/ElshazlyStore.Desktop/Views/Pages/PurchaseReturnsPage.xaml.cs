using System.Windows;
using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class PurchaseReturnsPage : UserControl
{
    public PurchaseReturnsPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.PurchaseReturnsViewModel vm)
        {
            await vm.InitializeCommand.ExecuteAsync(null);
            if (vm.Items.Count == 0 && !vm.IsLoading)
                await vm.LoadCommand.ExecuteAsync(null);
        }
    }
}

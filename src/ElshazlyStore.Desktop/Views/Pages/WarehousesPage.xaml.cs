using System.Windows;
using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class WarehousesPage : UserControl
{
    public WarehousesPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.WarehousesViewModel vm && vm.Items.Count == 0 && !vm.IsLoading)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}

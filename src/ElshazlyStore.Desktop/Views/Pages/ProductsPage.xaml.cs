using System.Windows.Controls;
using System.Windows.Input;
using ElshazlyStore.Desktop.Models.Dtos;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class ProductsPage : UserControl
{
    public ProductsPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ProductsViewModel vm && vm.Items.Count == 0 && !vm.IsLoading)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid
            && grid.SelectedItem is ProductDto product
            && DataContext is ViewModels.ProductsViewModel vm)
        {
            vm.ViewDetailsCommand.Execute(product);
        }
    }
}

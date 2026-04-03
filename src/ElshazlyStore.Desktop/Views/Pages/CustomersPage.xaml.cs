using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class CustomersPage : UserControl
{
    public CustomersPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.CustomersViewModel vm && vm.Items.Count == 0 && !vm.IsLoading)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}

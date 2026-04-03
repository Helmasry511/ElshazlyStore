using System.Windows.Controls;
using System.Windows.Input;
using ElshazlyStore.Desktop.Models.Dtos;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class VariantsPage : UserControl
{
    public VariantsPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.VariantsViewModel vm && vm.Items.Count == 0 && !vm.IsLoading)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.Item is VariantListDto variant
            && DataContext is ViewModels.VariantsViewModel vm)
        {
            vm.ShowBalanceDetailsCommand.Execute(variant);
        }
    }
}

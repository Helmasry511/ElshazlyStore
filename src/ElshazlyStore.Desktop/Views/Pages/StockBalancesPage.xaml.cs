using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class StockBalancesPage : UserControl
{
    public StockBalancesPage()
    {
        InitializeComponent();
    }

    private bool _loaded;
    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;

        if (DataContext is ViewModels.StockBalancesViewModel vm)
            await vm.InitializeCommand.ExecuteAsync(null);
    }
}

using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class StockMovementsPage : UserControl
{
    public StockMovementsPage()
    {
        InitializeComponent();
    }

    private bool _loaded;
    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;

        if (DataContext is ViewModels.StockMovementsViewModel vm)
            await vm.InitializeCommand.ExecuteAsync(null);
    }

    private void OnLineVariantSearchFocused(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is ViewModels.MovementLineVm line
            && DataContext is ViewModels.StockMovementsViewModel vm)
        {
            vm.StartLineVariantSearchCommand.Execute(line);
        }
    }
}

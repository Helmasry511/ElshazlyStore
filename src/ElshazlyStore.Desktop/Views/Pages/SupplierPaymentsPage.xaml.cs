using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class SupplierPaymentsPage : UserControl
{
    public SupplierPaymentsPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.SupplierPaymentsViewModel vm)
            vm.InitializeCommand.Execute(null);
    }
}

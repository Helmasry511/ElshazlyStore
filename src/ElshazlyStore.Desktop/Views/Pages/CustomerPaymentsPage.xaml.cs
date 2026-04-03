using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class CustomerPaymentsPage : UserControl
{
    public CustomerPaymentsPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.CustomerPaymentsViewModel vm)
            vm.InitializeCommand.Execute(null);
    }
}

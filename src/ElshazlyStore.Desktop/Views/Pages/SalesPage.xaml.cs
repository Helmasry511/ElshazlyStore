using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ElshazlyStore.Desktop.Views.Pages;

public partial class SalesPage : UserControl
{
    public SalesPage()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.SalesViewModel vm)
        {
            vm.ClearTransientStatusForNavigation();
            await vm.InitializeCommand.ExecuteAsync(null);
            if (vm.Items.Count == 0 && !vm.IsLoading)
                await vm.LoadCommand.ExecuteAsync(null);

            UpdateSortGlyph(vm.SortColumn, vm.SortDescending);
        }
    }

    private async void OnSalesGridSorting(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is not ViewModels.SalesViewModel vm)
            return;

        var sortColumn = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortColumn))
            return;

        e.Handled = true;
        await vm.ApplyGridSortCommand.ExecuteAsync(sortColumn);
        UpdateSortGlyph(vm.SortColumn, vm.SortDescending);
    }

    private void UpdateSortGlyph(string? sortColumn, bool isDescending)
    {
        foreach (var column in SalesGrid.Columns)
        {
            if (!string.Equals(column.SortMemberPath, sortColumn, StringComparison.Ordinal))
            {
                column.SortDirection = null;
                continue;
            }

            column.SortDirection = isDescending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
    }
}
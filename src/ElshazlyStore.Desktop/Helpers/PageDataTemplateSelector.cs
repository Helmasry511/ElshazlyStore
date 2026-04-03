using System.Windows;
using System.Windows.Controls;
using ElshazlyStore.Desktop.ViewModels;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Selects the correct DataTemplate for each ViewModel type shown in the content region.
/// </summary>
public sealed class PageDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HomeTemplate { get; set; }
    public DataTemplate? SettingsTemplate { get; set; }
    public DataTemplate? ProductsTemplate { get; set; }
    public DataTemplate? VariantsTemplate { get; set; }
    public DataTemplate? CustomersTemplate { get; set; }
    public DataTemplate? SuppliersTemplate { get; set; }
    public DataTemplate? WarehousesTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item switch
        {
            HomeViewModel => HomeTemplate,
            SettingsViewModel => SettingsTemplate,
            ProductsViewModel => ProductsTemplate,
            VariantsViewModel => VariantsTemplate,
            CustomersViewModel => CustomersTemplate,
            SuppliersViewModel => SuppliersTemplate,
            WarehousesViewModel => WarehousesTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}

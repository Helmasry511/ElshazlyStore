using CommunityToolkit.Mvvm.ComponentModel;

namespace ElshazlyStore.Desktop.ViewModels;

/// <summary>
/// Base class for all ViewModels. Uses CommunityToolkit.Mvvm source generators.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isBusy;
}

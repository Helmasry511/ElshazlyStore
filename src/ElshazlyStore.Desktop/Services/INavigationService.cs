using ElshazlyStore.Desktop.ViewModels;

namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Simple page-level navigation service for the shell content region.
/// </summary>
public interface INavigationService
{
    ViewModelBase? CurrentPage { get; }
    event Action<ViewModelBase>? CurrentPageChanged;

    /// <summary>Navigate to a cached VM instance (skips if already on that page).</summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>
    /// Navigate to a freshly-created VM, apply <paramref name="configure"/> before displaying.
    /// Always navigates (replaces any cached instance), enabling context-aware pre-filtering.
    /// </summary>
    void NavigateTo<TViewModel>(Action<TViewModel> configure) where TViewModel : ViewModelBase;
}

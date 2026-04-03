using System.Collections.Concurrent;
using ElshazlyStore.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ElshazlyStore.Desktop.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Type, ViewModelBase> _cache = new();

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ViewModelBase? CurrentPage { get; private set; }

    public event Action<ViewModelBase>? CurrentPageChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var vmType = typeof(TViewModel);

        // If already on this page, do nothing (prevents data loss on re-click)
        if (CurrentPage is not null && CurrentPage.GetType() == vmType)
            return;

        var viewModel = _cache.GetOrAdd(vmType, _ => _serviceProvider.GetRequiredService<TViewModel>());
        CurrentPage = viewModel;
        CurrentPageChanged?.Invoke(viewModel);
    }

    public void NavigateTo<TViewModel>(Action<TViewModel> configure) where TViewModel : ViewModelBase
    {
        var vmType = typeof(TViewModel);

        // Create a fresh instance and replace any cached one so configure takes full effect
        _cache.TryRemove(vmType, out _);
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        configure(viewModel);
        _cache[vmType] = viewModel;

        CurrentPage = viewModel;
        CurrentPageChanged?.Invoke(viewModel);
    }
}

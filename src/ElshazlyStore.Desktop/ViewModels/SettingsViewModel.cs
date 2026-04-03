using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Services;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private string _apiBaseUrl = string.Empty;

    public SettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        Title = Localization.Strings.Settings_Title;
        _isDarkMode = _themeService.IsDarkMode;
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        _themeService.SetTheme(value);
    }
}

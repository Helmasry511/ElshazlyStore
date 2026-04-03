using System.Windows;

namespace ElshazlyStore.Desktop.Services;

public sealed class ThemeService : IThemeService
{
    private const string DarkThemeUri = "Resources/Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Resources/Themes/LightTheme.xaml";

    private readonly IUserPreferencesService _preferences;

    public ThemeService(IUserPreferencesService preferences)
    {
        _preferences = preferences;
        IsDarkMode = _preferences.IsDarkMode;
    }

    public bool IsDarkMode { get; private set; }

    public event Action<bool>? ThemeChanged;

    public void SetTheme(bool isDark)
    {
        IsDarkMode = isDark;
        _preferences.IsDarkMode = isDark;
        _preferences.Save();

        ApplyTheme(isDark);
        ThemeChanged?.Invoke(isDark);
    }

    /// <summary>
    /// Apply the theme to the running application by swapping the first merged dictionary.
    /// </summary>
    public void ApplyTheme(bool isDark)
    {
        var app = Application.Current;
        if (app is null) return;

        var mergedDicts = app.Resources.MergedDictionaries;
        var themeUri = new Uri(isDark ? DarkThemeUri : LightThemeUri, UriKind.Relative);
        var newTheme = new ResourceDictionary { Source = themeUri };

        // The theme dictionary is always at index 0
        if (mergedDicts.Count > 0)
        {
            mergedDicts[0] = newTheme;
        }
        else
        {
            mergedDicts.Insert(0, newTheme);
        }
    }
}

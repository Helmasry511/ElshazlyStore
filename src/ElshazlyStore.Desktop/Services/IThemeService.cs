namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Manages theme switching between Dark and Light modes.
/// </summary>
public interface IThemeService
{
    bool IsDarkMode { get; }
    void SetTheme(bool isDark);
    event Action<bool>? ThemeChanged;
}

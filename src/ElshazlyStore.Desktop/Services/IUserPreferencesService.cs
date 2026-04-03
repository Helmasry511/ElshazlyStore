namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Persists user preferences (theme, locale, etc.) to a local JSON file.
/// </summary>
public interface IUserPreferencesService
{
    bool IsDarkMode { get; set; }
    void Save();
    void Load();
}

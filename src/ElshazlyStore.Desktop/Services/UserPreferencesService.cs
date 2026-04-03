using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.Services;

public sealed class UserPreferencesService : IUserPreferencesService
{
    private static readonly string PreferencesDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ElshazlyStore");

    private static readonly string PreferencesFile =
        Path.Combine(PreferencesDir, "preferences.json");

    private readonly ILogger<UserPreferencesService> _logger;

    public bool IsDarkMode { get; set; } = true; // Default: dark

    public UserPreferencesService(ILogger<UserPreferencesService> logger)
    {
        _logger = logger;
        Load();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(PreferencesDir);
            var data = new PreferencesData { IsDarkMode = IsDarkMode };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PreferencesFile, json);
            _logger.LogDebug("Preferences saved to {Path}", PreferencesFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save preferences");
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(PreferencesFile)) return;

            var json = File.ReadAllText(PreferencesFile);
            var data = JsonSerializer.Deserialize<PreferencesData>(json);
            if (data is not null)
            {
                IsDarkMode = data.IsDarkMode;
            }
            _logger.LogDebug("Preferences loaded from {Path}", PreferencesFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load preferences, using defaults");
        }
    }

    private sealed class PreferencesData
    {
        public bool IsDarkMode { get; set; } = true;
    }
}

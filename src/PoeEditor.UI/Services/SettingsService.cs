using System.IO;
using System.Text.Json;

namespace PoeEditor.UI.Services;

/// <summary>
/// Application settings that are persisted between sessions.
/// </summary>
public class AppSettings
{
    // Elasticsearch settings
    public string ElasticsearchUrl { get; set; } = "http://localhost:9200";
    public string ElasticsearchIndexName { get; set; } = "poe-files";
    public string ElasticsearchUsername { get; set; } = "";
    public string ElasticsearchPassword { get; set; } = "";

    // Path settings
    public string LastArchivePath { get; set; } = "";
    public string LastFolderPath { get; set; } = "";
    public string LastOutputPath { get; set; } = "";
}

/// <summary>
/// Service for persisting application settings.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoeEditorPatcher");

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads settings from disk. Returns default settings if file doesn't exist.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Return default settings on error
        }

        return new AppSettings();
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silently fail - settings are not critical
        }
    }
}

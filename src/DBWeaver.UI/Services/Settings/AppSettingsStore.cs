using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DBWeaver.UI.Services.Settings;

public sealed class AppSettings
{
    public string ThemeVariant { get; set; } = "Dark";
    public ShortcutSettingsSection Shortcuts { get; set; } = new();
}

public static class AppSettingsStore
{
    private static readonly ILogger _logger = NullLogger.Instance;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string? SettingsPathOverride { get; set; }

    private static string SettingsPath => SettingsPathOverride ?? Path.Combine(
        global::DBWeaver.UI.AppConstants.AppDataDirectory,
        "app.settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            string json = File.ReadAllText(SettingsPath);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            settings.Shortcuts ??= new ShortcutSettingsSection();
            settings.Shortcuts.Overrides ??= [];
            if (settings.Shortcuts.Version <= 0)
                settings.Shortcuts.Version = 1;
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to load app settings. Falling back to defaults.");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        _ = TrySave(settings);
    }

    public static bool TrySave(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            string json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsPath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Failed to persist app settings (best effort).");
            return false;
        }
    }

    public static void SaveThemeVariant(string variant)
    {
        AppSettings settings = Load();
        settings.ThemeVariant = string.IsNullOrWhiteSpace(variant) ? "Dark" : variant;
        Save(settings);
    }
}

using System.ComponentModel;
using System.Text.Json;

namespace VisualSqlArchitect.UI.Services.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string DefaultCulture = "pt-BR";
    private readonly object _sync = new();
    private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentCulture { get; private set; } = DefaultCulture;

    public string CurrentLanguageLabel => CurrentCulture.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)
        ? "PT-BR"
        : "EN-US";

    public string this[string key]
    {
        get
        {
            lock (_sync)
            {
                return _strings.TryGetValue(key, out string? value) ? value : key;
            }
        }
    }

    private LocalizationService()
    {
        string culture = LoadSavedCulture() ?? DefaultCulture;
        SetCulture(culture);
    }

    public bool ToggleCulture()
    {
        string target = CurrentCulture.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "pt-BR";
        return SetCulture(target);
    }

    public bool SetCulture(string culture)
    {
        string normalized = NormalizeCulture(culture);
        Dictionary<string, string>? loaded = LoadFromJson(normalized);
        if (loaded is null)
            return false;

        lock (_sync)
        {
            _strings = loaded;
            CurrentCulture = normalized;
        }

        SaveCulture(normalized);
        RaiseAllChanged();
        return true;
    }

    private static string NormalizeCulture(string? culture)
    {
        if (string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(culture, "en-US", StringComparison.OrdinalIgnoreCase))
            return "en-US";

        return "pt-BR";
    }

    private static Dictionary<string, string>? LoadFromJson(string culture)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string path = Path.Combine(baseDir, "Assets", "Localization", $"{culture}.json");
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VisualSqlArchitect",
        "localization.settings.json");

    private static string? LoadSavedCulture()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return null;

            string json = File.ReadAllText(SettingsFilePath);
            Dictionary<string, string>? payload = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (payload is null)
                return null;

            return payload.TryGetValue("culture", out string? culture) ? culture : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCulture(string culture)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            string json = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["culture"] = culture
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // best effort
        }
    }

    private void RaiseAllChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageLabel)));
    }
}

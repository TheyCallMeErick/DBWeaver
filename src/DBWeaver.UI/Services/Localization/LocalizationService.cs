using System.ComponentModel;
using System.Text.Json;

namespace DBWeaver.UI.Services.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private const string DefaultCulture = "pt-BR";
    private static readonly string[] SupportedCultures =
    {
        "pt-BR",
        "en-US",
        "es-ES",
        "ru-RU",
        "ja-JP",
        "zh-TW",
    };
    private readonly object _sync = new();
    private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentCulture { get; private set; } = DefaultCulture;

    public string CurrentLanguageLabel => CurrentCulture.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)
        ? "PT-BR"
        : CurrentCulture.ToUpperInvariant();

    public IReadOnlyList<string> AvailableCultures => SupportedCultures;

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
        if (string.IsNullOrWhiteSpace(culture))
            return DefaultCulture;

        string normalized = culture.Trim();
        if (string.Equals(normalized, "en", StringComparison.OrdinalIgnoreCase))
            normalized = "en-US";
        else if (string.Equals(normalized, "pt", StringComparison.OrdinalIgnoreCase))
            normalized = "pt-BR";
        else if (string.Equals(normalized, "es", StringComparison.OrdinalIgnoreCase))
            normalized = "es-ES";
        else if (string.Equals(normalized, "ru", StringComparison.OrdinalIgnoreCase))
            normalized = "ru-RU";
        else if (string.Equals(normalized, "ja", StringComparison.OrdinalIgnoreCase))
            normalized = "ja-JP";
        else if (string.Equals(normalized, "zh-TW", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(normalized, "zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(normalized, "zh", StringComparison.OrdinalIgnoreCase))
            normalized = "zh-TW";

        foreach (string supported in SupportedCultures)
        {
            if (string.Equals(normalized, supported, StringComparison.OrdinalIgnoreCase))
                return supported;
        }

        return DefaultCulture;
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
        global::DBWeaver.UI.AppConstants.AppDataDirectory,
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

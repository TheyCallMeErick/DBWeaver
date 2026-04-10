using System.Text.Json;

namespace DBWeaver.UI.Services;

/// <summary>
/// Persists favorite template names for Start Menu quick access.
/// </summary>
public static class TemplateFavoritesStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string AppDataDir =>
        global::DBWeaver.UI.AppConstants.AppDataDirectory;

    private static string FavoritesFilePath => Path.Combine(AppDataDir, "template-favorites.json");

    public static HashSet<string> Load()
    {
        try
        {
            if (!File.Exists(FavoritesFilePath))
                return new(StringComparer.OrdinalIgnoreCase);

            string json = File.ReadAllText(FavoritesFilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
            return new HashSet<string>(list.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Save(IEnumerable<string> templateNames)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var names = templateNames
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            File.WriteAllText(FavoritesFilePath, JsonSerializer.Serialize(names, JsonOpts));
        }
        catch
        {
            // Best effort persistence.
        }
    }
}

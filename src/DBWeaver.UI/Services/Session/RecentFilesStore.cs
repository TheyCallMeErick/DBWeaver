using System.Text.Json;

namespace DBWeaver.UI.Services;

public sealed record RecentFileEntry(string FilePath, DateTime LastOpenedUtc);

/// <summary>
/// Persists a lightweight MRU list for Start Menu recent files.
/// </summary>
public static class RecentFilesStore
{
    private const int MaxEntries = 20;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string AppDataDir =>
        global::DBWeaver.UI.AppConstants.AppDataDirectory;

    private static string RecentFilePath => Path.Combine(AppDataDir, "recent-files.json");

    public static IReadOnlyList<RecentFileEntry> GetRecent(int max = 6)
    {
        try
        {
            if (!File.Exists(RecentFilePath))
                return [];

            var json = File.ReadAllText(RecentFilePath);
            var all = JsonSerializer.Deserialize<List<RecentFileEntry>>(json, JsonOpts) ?? [];

            return all
                .Where(x => !string.IsNullOrWhiteSpace(x.FilePath) && File.Exists(x.FilePath))
                .OrderByDescending(x => x.LastOpenedUtc)
                .Take(Math.Max(1, max))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static void Touch(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            Directory.CreateDirectory(AppDataDir);

            List<RecentFileEntry> items = GetRecent(MaxEntries).ToList();
            items.RemoveAll(x => string.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            items.Insert(0, new RecentFileEntry(filePath, DateTime.UtcNow));

            items = items
                .OrderByDescending(x => x.LastOpenedUtc)
                .Take(MaxEntries)
                .ToList();

            File.WriteAllText(RecentFilePath, JsonSerializer.Serialize(items, JsonOpts));
        }
        catch
        {
            // Best effort persistence.
        }
    }
}

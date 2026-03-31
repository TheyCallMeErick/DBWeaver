using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace VisualSqlArchitect.UI.Serialization;

// ═════════════════════════════════════════════════════════════════════════════
// MODEL
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A single versioned snapshot of the canvas state.
/// </summary>
public record FlowVersion(
    string Id,
    string Label,
    string CreatedAt,
    int NodeCount,
    int ConnectionCount,
    /// <summary>Full serialized canvas JSON (SavedCanvas).</summary>
    string CanvasJson
);

// ═════════════════════════════════════════════════════════════════════════════
// STORE
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Persists flow version checkpoints to
/// ~/.config/VisualSqlArchitect/flow_versions.json (Linux) or
/// %APPDATA%\VisualSqlArchitect\flow_versions.json (Windows).
///
/// Automatically caps history at <see cref="MaxVersions"/> entries to avoid
/// unbounded growth — oldest entries are pruned when the cap is reached.
/// </summary>
public static class FlowVersionStore
{
    public const int MaxVersions = 50;

    public static event Action<string>? WarningRaised;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string StorePath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualSqlArchitect"
        );
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "flow_versions.json");
    }

    /// <summary>Loads all versions from disk (newest first). Returns empty list on first run.</summary>
    public static List<FlowVersion> Load()
    {
        string path = StorePath();
        if (!File.Exists(path))
            return [];
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<FlowVersion>>(json, _opts) ?? [];
        }
        catch (Exception ex)
        {
            RaiseWarning($"Failed to load flow versions from '{path}': {ex.Message}");
            return [];
        }
    }

    /// <summary>Overwrites the store with the given list.</summary>
    public static void Save(List<FlowVersion> versions)
    {
        try
        {
            File.WriteAllText(StorePath(), JsonSerializer.Serialize(versions, _opts));
        }
        catch (Exception ex)
        {
            RaiseWarning($"Failed to save flow versions: {ex.Message}");
        }
    }

    private static void RaiseWarning(string message)
    {
        Debug.WriteLine($"[FlowVersionStore] {message}");
        WarningRaised?.Invoke(message);
    }

    /// <summary>
    /// Prepends a new version to the history and persists.
    /// Prunes oldest entries beyond <see cref="MaxVersions"/>.
    /// </summary>
    public static void Add(FlowVersion version)
    {
        List<FlowVersion> all = Load();
        all.Insert(0, version);
        if (all.Count > MaxVersions)
            all.RemoveRange(MaxVersions, all.Count - MaxVersions);
        Save(all);
    }

    /// <summary>Removes a version by ID.</summary>
    public static void Remove(string id)
    {
        List<FlowVersion> all = Load();
        all.RemoveAll(v => v.Id == id);
        Save(all);
    }
}

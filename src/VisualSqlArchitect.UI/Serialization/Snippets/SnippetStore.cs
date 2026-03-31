using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace VisualSqlArchitect.UI.Serialization;

// ═════════════════════════════════════════════════════════════════════════════
// SNIPPET MODEL
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A saved subgraph snippet — a named, reusable group of nodes and their
/// internal connections that can be inserted into any canvas.
/// </summary>
public record SavedSnippet(
    string Id,
    string Name,
    string? Tags,
    string? Description,
    string CreatedAt,
    List<SavedNode> Nodes,
    List<SavedConnection> Connections
);

// ═════════════════════════════════════════════════════════════════════════════
// SNIPPET STORE  (JSON persistence in the user's app data folder)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Persists snippets to a JSON file in the OS-appropriate application data
/// directory (~/.config/VisualSqlArchitect/snippets.json on Linux,
/// %APPDATA%\VisualSqlArchitect\snippets.json on Windows).
/// </summary>
public static class SnippetStore
{
    public static event Action<string>? WarningRaised;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string StoreFilePath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualSqlArchitect"
        );
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "snippets.json");
    }

    /// <summary>Loads all snippets from disk. Returns an empty list on first run or error.</summary>
    public static List<SavedSnippet> Load()
    {
        string path = StoreFilePath();
        if (!File.Exists(path))
            return [];
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<SavedSnippet>>(json, _opts) ?? [];
        }
        catch (Exception ex)
        {
            RaiseWarning($"Failed to load snippets from '{path}': {ex.Message}");
            return [];
        }
    }

    /// <summary>Overwrites the snippet store with the given list.</summary>
    public static void Save(List<SavedSnippet> snippets)
    {
        try
        {
            File.WriteAllText(StoreFilePath(), JsonSerializer.Serialize(snippets, _opts));
        }
        catch (Exception ex)
        {
            RaiseWarning($"Failed to save snippets: {ex.Message}");
        }
    }

    private static void RaiseWarning(string message)
    {
        Debug.WriteLine($"[SnippetStore] {message}");
        WarningRaised?.Invoke(message);
    }

    /// <summary>Appends a new snippet and persists to disk.</summary>
    public static void Add(SavedSnippet snippet)
    {
        List<SavedSnippet> all = Load();
        all.Add(snippet);
        Save(all);
    }

    /// <summary>Removes a snippet by ID and persists to disk.</summary>
    public static void Remove(string id)
    {
        List<SavedSnippet> all = Load();
        all.RemoveAll(s => s.Id == id);
        Save(all);
    }
}

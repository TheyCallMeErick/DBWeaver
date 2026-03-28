using VisualSqlArchitect.UI.Serialization;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Lightweight view model for displaying a saved snippet in the search menu.
/// </summary>
public sealed class SnippetViewModel(SavedSnippet snippet)
{
    public SavedSnippet Snippet { get; } = snippet;

    public string Name => snippet.Name;
    public string? Tags => snippet.Tags;
    public string Summary => $"{snippet.Nodes.Count} node{(snippet.Nodes.Count == 1 ? "" : "s")}";
    public bool HasTags => !string.IsNullOrWhiteSpace(snippet.Tags);
}

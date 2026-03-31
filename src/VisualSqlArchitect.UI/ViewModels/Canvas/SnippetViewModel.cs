using VisualSqlArchitect.UI.Serialization;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Lightweight view model for displaying a saved snippet in the search menu.
/// </summary>
public sealed class SnippetViewModel(SavedSnippet snippet)
{
    public SavedSnippet Snippet { get; } = snippet;

    public string Name => Snippet.Name;
    public string? Tags => Snippet.Tags;
    public string Summary => $"{Snippet.Nodes.Count} node{(Snippet.Nodes.Count == 1 ? "" : "s")}";
    public bool HasTags => !string.IsNullOrWhiteSpace(Snippet.Tags);
}

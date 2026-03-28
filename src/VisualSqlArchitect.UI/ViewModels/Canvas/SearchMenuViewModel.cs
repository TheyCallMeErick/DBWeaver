using System.Collections.ObjectModel;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Serialization;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Manages the search menu for node/table discovery and spawning.
/// Filters and displays node definitions and database tables for quick access.
/// </summary>
public sealed class SearchMenuViewModel : ViewModelBase
{
    private string _query = "";
    private bool _isVisible;
    private Point _spawnPos;
    private NodeSearchResultViewModel? _selected;

    /// <summary>
    /// The search query entered by the user.
    /// Triggers filtering when changed.
    /// </summary>
    public string Query
    {
        get => _query;
        set
        {
            Set(ref _query, value);
            FilterResults();
        }
    }

    /// <summary>
    /// Whether the search menu is currently visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    /// <summary>
    /// The canvas position where the menu was spawned.
    /// </summary>
    public Point SpawnPosition
    {
        get => _spawnPos;
        set => Set(ref _spawnPos, value);
    }

    /// <summary>
    /// The currently selected search result.
    /// </summary>
    public NodeSearchResultViewModel? SelectedResult
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    /// <summary>
    /// Observable collection of filtered search results (nodes and tables).
    /// </summary>
    public ObservableCollection<NodeSearchResultViewModel> Results { get; } = [];

    /// <summary>
    /// Saved snippets visible when the query is empty or matches the snippet name/tags.
    /// </summary>
    public ObservableCollection<SnippetViewModel> Snippets { get; } = [];

    public bool HasSnippets => Snippets.Count > 0;

    private static readonly IReadOnlyList<NodeDefinition> AllDefs = NodeDefinitionRegistry
        .All.OrderBy(d => d.Category)
        .ThenBy(d => d.DisplayName)
        .ToList();

    private readonly List<NodeSearchResultViewModel> _tables = [];

    /// <summary>
    /// Loads available database tables into the search results.
    /// </summary>
    /// <param name="tables">Enumerable of (FullName, Columns) tuples</param>
    public void LoadTables(
        IEnumerable<(string FullName, IReadOnlyList<(string Name, PinDataType Type)> Cols)> tables
    )
    {
        _tables.Clear();
        _tables.AddRange(
            tables.Select(t => NodeSearchResultViewModel.ForTable(t.FullName, t.Cols))
        );
        FilterResults();
    }

    /// <summary>
    /// Reloads snippets from the persistent store and updates the Snippets collection.
    /// Call after adding or removing a snippet.
    /// </summary>
    public void LoadSnippets()
    {
        Snippets.Clear();
        string q = Query.Trim();
        foreach (SavedSnippet s in SnippetStore.Load())
        {
            if (
                string.IsNullOrEmpty(q)
                || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (s.Tags ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            )
                Snippets.Add(new SnippetViewModel(s));
        }
        RaisePropertyChanged(nameof(HasSnippets));
    }

    /// <summary>
    /// Opens the search menu at the specified canvas position.
    /// </summary>
    public void Open(Point pos)
    {
        SpawnPosition = pos;
        Query = "";
        LoadSnippets();
        FilterResults();
        IsVisible = true;
    }

    /// <summary>
    /// Closes the search menu and clears the query.
    /// </summary>
    public void Close()
    {
        IsVisible = false;
        Query = "";
    }

    /// <summary>
    /// Filters results based on current query.
    /// Shows up to 5 tables and up to 12 total results.
    /// </summary>
    private void FilterResults()
    {
        Results.Clear();
        LoadSnippets();
        string q = Query.Trim();

        // Tables first (up to 5)
        IEnumerable<NodeSearchResultViewModel> filteredTables = string.IsNullOrEmpty(q)
            ? _tables
            : _tables.Where(t =>
                t.TableFullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            );
        foreach (NodeSearchResultViewModel? t in filteredTables.Take(5))
            Results.Add(t);

        // Then node definitions (fill remaining slots up to 12 total)
        IEnumerable<NodeDefinition> filteredNodes = string.IsNullOrEmpty(q)
            ? AllDefs
            : AllDefs.Where(d =>
                d.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || d.Category.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
            );
        foreach (NodeDefinition? d in filteredNodes.Take(12 - Results.Count))
            Results.Add(new NodeSearchResultViewModel(d));

        SelectedResult = Results.FirstOrDefault();
    }

    /// <summary>
    /// Selects the next result in the list (wraps around).
    /// </summary>
    public void SelectNext()
    {
        if (Results.Count == 0)
            return;
        SelectedResult = Results[(Results.IndexOf(SelectedResult!) + 1) % Results.Count];
    }

    /// <summary>
    /// Selects the previous result in the list (wraps around).
    /// </summary>
    public void SelectPrev()
    {
        if (Results.Count == 0)
            return;
        SelectedResult = Results[
            (Results.IndexOf(SelectedResult!) - 1 + Results.Count) % Results.Count
        ];
    }
}

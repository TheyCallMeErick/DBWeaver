using System.Collections.ObjectModel;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Command item ─────────────────────────────────────────────────────────────

public sealed class PaletteCommandItem
{
    public string Name        { get; init; } = "";
    public string Description { get; init; } = "";
    public string Shortcut    { get; init; } = "";
    public string Icon        { get; init; } = "▶";
    public Action Execute     { get; init; } = () => { };
}

// ── ViewModel ────────────────────────────────────────────────────────────────

public sealed class CommandPaletteViewModel : ViewModelBase
{
    private bool   _isVisible;
    private string _query         = "";
    private int    _selectedIndex = 0;

    private readonly List<PaletteCommandItem> _all = [];

    public ObservableCollection<PaletteCommandItem> Results { get; } = [];

    public bool IsVisible
    { get => _isVisible; set => Set(ref _isVisible, value); }

    public string Query
    { get => _query; set { Set(ref _query, value); Filter(); } }

    public int SelectedIndex
    { get => _selectedIndex; set => Set(ref _selectedIndex, value); }

    // ── Registration ─────────────────────────────────────────────────────────

    public void RegisterCommands(IEnumerable<PaletteCommandItem> commands)
    {
        _all.AddRange(commands);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Open()
    {
        Query         = "";
        SelectedIndex = 0;
        Filter();
        IsVisible = true;
    }

    public void Close()
    {
        IsVisible = false;
        Query     = "";
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public void SelectNext()
    {
        if (Results.Count > 0)
            SelectedIndex = (SelectedIndex + 1) % Results.Count;
    }

    public void SelectPrev()
    {
        if (Results.Count > 0)
            SelectedIndex = (SelectedIndex - 1 + Results.Count) % Results.Count;
    }

    public void ExecuteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Results.Count) return;
        var cmd = Results[SelectedIndex];
        Close();
        cmd.Execute();
    }

    // ── Filtering ────────────────────────────────────────────────────────────

    private void Filter()
    {
        Results.Clear();
        SelectedIndex = 0;
        var q = _query.Trim();
        var matches = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(c =>
                c.Name.Contains(q,        StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var m in matches)
            Results.Add(m);
    }
}

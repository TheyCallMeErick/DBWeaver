using System.Collections.ObjectModel;
using DBWeaver.UI.Services.CommandPalette;

namespace DBWeaver.UI.ViewModels;

// ── ViewModel ────────────────────────────────────────────────────────────────

public sealed class CommandPaletteViewModel : ViewModelBase
{
    private bool _isVisible;
    private string _query = "";
    private int _selectedIndex = 0;
    private readonly ICommandPaletteFilterService _filterService;

    private readonly List<PaletteCommandItem> _all = [];

    public ObservableCollection<PaletteCommandItem> Results { get; } = [];

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public string Query
    {
        get => _query;
        set
        {
            Set(ref _query, value);
            Filter();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Set(ref _selectedIndex, value);
    }

    public CommandPaletteViewModel(ICommandPaletteFilterService? filterService = null)
    {
        _filterService = filterService ?? new CommandPaletteFilterService();
    }

    // ── Registration ─────────────────────────────────────────────────────────

    public void RegisterCommands(IEnumerable<PaletteCommandItem> commands) =>
        _all.AddRange(commands);

    public void SetCommands(IEnumerable<PaletteCommandItem> commands)
    {
        _all.Clear();
        _all.AddRange(commands);
        Filter();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Open()
    {
        Query = "";
        SelectedIndex = 0;
        Filter();
        IsVisible = true;
    }

    public void Close()
    {
        IsVisible = false;
        Query = "";
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
        if (SelectedIndex < 0 || SelectedIndex >= Results.Count)
            return;
        PaletteCommandItem cmd = Results[SelectedIndex];
        Close();
        cmd.Execute();
    }

    // ── Fuzzy filtering ───────────────────────────────────────────────────────

    private void Filter()
    {
        Results.Clear();
        SelectedIndex = 0;
        IReadOnlyList<PaletteCommandItem> filtered = _filterService.FilterAndSort(_all, _query);

        foreach (PaletteCommandItem cmd in filtered)
            Results.Add(cmd);
    }
}

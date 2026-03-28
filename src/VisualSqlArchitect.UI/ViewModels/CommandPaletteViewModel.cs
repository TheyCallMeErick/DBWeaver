using System.Collections.ObjectModel;
using Material.Icons;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Command item ─────────────────────────────────────────────────────────────

public sealed class PaletteCommandItem
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Shortcut { get; init; } = "";
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.Play;

    /// <summary>
    /// Extra keywords (space-separated) that improve discoverability in fuzzy search.
    /// Example: a "Cleanup Orphans" command can have Tags = "delete remove unused nodes".
    /// </summary>
    public string Tags { get; init; } = "";

    public Action Execute { get; init; } = () => { };
}

// ── Fuzzy scorer ─────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight fuzzy matching algorithm.
///
/// Scoring tiers (higher = better match):
///   400 — exact match of Name (case-insensitive)
///   300 — Name starts with query
///   200 — all query chars appear in Name in order (subsequence match)
///   100 — query found in Description or Tags (Contains, case-insensitive)
///     0 — no match
/// Within each tier, a bonus is added for matches that cluster the query chars
/// close together (shorter "span" = higher bonus).
/// </summary>
internal static class FuzzyScorer
{
    public static int Score(PaletteCommandItem item, string query)
    {
        if (string.IsNullOrEmpty(query))
            return 1; // show all when query is empty

        string q = query.ToLowerInvariant();
        string name = item.Name.ToLowerInvariant();
        string desc = item.Description.ToLowerInvariant();
        string tags = item.Tags.ToLowerInvariant();

        // Tier 1 – exact
        if (name == q)
            return 400;

        // Tier 2 – prefix
        if (name.StartsWith(q))
            return 300 + (100 - Math.Min(name.Length, 100));

        // Tier 3 – subsequence match on Name
        int span = SubsequenceSpan(name, q);
        if (span >= 0)
        {
            // Shorter span = higher bonus (max bonus 99 when span == q.Length)
            int bonus = Math.Max(0, 99 - (span - q.Length));
            return 200 + bonus;
        }

        // Tier 4 – substring match on description or tags
        if (desc.Contains(q) || tags.Contains(q))
            return 100;

        // Tier 5 – subsequence match on description or tags
        if (SubsequenceSpan(desc, q) >= 0 || SubsequenceSpan(tags, q) >= 0)
            return 50;

        return 0; // no match
    }

    /// <summary>
    /// Returns the length of the shortest substring of <paramref name="text"/>
    /// that contains all characters of <paramref name="pattern"/> as a subsequence,
    /// or -1 if no such substring exists.
    /// </summary>
    private static int SubsequenceSpan(string text, string pattern)
    {
        int pi = 0; // pattern index
        int start = -1;

        for (int ti = 0; ti < text.Length; ti++)
        {
            if (text[ti] != pattern[pi])
                continue;

            if (pi == 0)
                start = ti;
            pi++;

            if (pi == pattern.Length)
                return ti - start + 1; // span length
        }

        return -1;
    }
}

// ── ViewModel ────────────────────────────────────────────────────────────────

public sealed class CommandPaletteViewModel : ViewModelBase
{
    private bool _isVisible;
    private string _query = "";
    private int _selectedIndex = 0;

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

    // ── Registration ─────────────────────────────────────────────────────────

    public void RegisterCommands(IEnumerable<PaletteCommandItem> commands) =>
        _all.AddRange(commands);

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
        string q = _query.Trim();

        IOrderedEnumerable<(PaletteCommandItem Command, int Score)> scored = _all.Select(c =>
                (Command: c, Score: FuzzyScorer.Score(c, q))
            )
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Command.Name);

        foreach ((PaletteCommandItem cmd, int _) in scored)
            Results.Add(cmd);
    }
}

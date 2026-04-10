using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.UI.ViewModels.Shortcuts;

/// <summary>
/// Shared shortcuts presentation and editing facade for F1 and Settings.
/// </summary>
public sealed class KeyboardShortcutsViewModel
{
    private readonly IShortcutRegistry _shortcutRegistry;
    private IReadOnlyList<ShortcutItemViewModel> _allItems = [];
    private string _searchText = string.Empty;

    public KeyboardShortcutsViewModel(IShortcutRegistry shortcutRegistry)
    {
        _shortcutRegistry = shortcutRegistry ?? throw new ArgumentNullException(nameof(shortcutRegistry));
        Refresh();
    }

    public IReadOnlyList<ShortcutSectionViewModel> Sections { get; private set; } = [];

    public IReadOnlyList<ShortcutValidationIssue> Issues { get; private set; } = [];

    public string SearchText => _searchText;

    public int TotalCount => _allItems.Count;

    public int FilteredCount => Sections.Sum(section => section.Items.Count);

    public ShortcutUpdateResult ApplyOverride(string actionId, string? gestureText)
    {
        ShortcutUpdateResult result = _shortcutRegistry.TryOverride(actionId, gestureText);
        Refresh();
        return result;
    }

    public ShortcutUpdateResult ResetShortcut(string actionId)
    {
        ShortcutUpdateResult result = _shortcutRegistry.ResetToDefault(actionId);
        Refresh();
        return result;
    }

    public ShortcutUpdateResult ResetAll()
    {
        ShortcutUpdateResult result = _shortcutRegistry.ResetAllToDefault();
        Refresh();
        return result;
    }

    public void SetSearchText(string? searchText)
    {
        _searchText = (searchText ?? string.Empty).Trim();
        RebuildSections();
    }

    public void Refresh()
    {
        ShortcutRegistrySnapshot snapshot = _shortcutRegistry.GetSnapshot();
        Issues = snapshot.Issues;
        _allItems = snapshot.Definitions
            .Select(definition =>
            {
                ShortcutValidationIssue? issue = snapshot.Issues.FirstOrDefault(item =>
                    string.Equals(item.ActionId, definition.ActionId.Value, StringComparison.OrdinalIgnoreCase));
                string effective = definition.EffectiveGesture?.DisplayText ?? string.Empty;
                string baseline = definition.DefaultGesture?.DisplayText ?? string.Empty;
                bool customized = !string.Equals(
                    definition.DefaultGesture?.NormalizedText,
                    definition.EffectiveGesture?.NormalizedText,
                    StringComparison.OrdinalIgnoreCase);

                return new ShortcutItemViewModel(
                    definition.ActionId.Value,
                    definition.Section,
                    definition.Name,
                    definition.Description,
                    effective,
                    baseline,
                    customized,
                    issue?.Code,
                    issue?.Message,
                    definition.Tags);
            })
            .OrderBy(item => item.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RebuildSections();
    }

    private void RebuildSections()
    {
        IEnumerable<ShortcutItemViewModel> filtered = _allItems;
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filtered = filtered.Where(item =>
                item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || item.Section.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || item.EffectiveGesture.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || item.DefaultGesture.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || item.Tags.Any(tag => tag.Contains(_searchText, StringComparison.OrdinalIgnoreCase)));
        }

        Sections = filtered
            .GroupBy(item => item.Section)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ShortcutSectionViewModel(group.Key, group.ToList()))
            .ToList();
    }
}

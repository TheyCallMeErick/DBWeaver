namespace DBWeaver.UI.ViewModels.Shortcuts;

/// <summary>
/// Presentation grouping for shortcut items by section.
/// </summary>
public sealed record ShortcutSectionViewModel(
    string Name,
    IReadOnlyList<ShortcutItemViewModel> Items);

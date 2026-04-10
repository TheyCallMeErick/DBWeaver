namespace DBWeaver.UI.ViewModels.Shortcuts;

/// <summary>
/// Presentation model for a single shortcut action.
/// </summary>
public sealed record ShortcutItemViewModel(
    string ActionId,
    string Section,
    string Name,
    string Description,
    string EffectiveGesture,
    string DefaultGesture,
    bool IsCustomized,
    string? IssueCode,
    string? IssueMessage,
    IReadOnlyList<string> Tags);

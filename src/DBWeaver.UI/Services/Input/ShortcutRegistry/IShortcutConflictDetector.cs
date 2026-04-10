namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Detects gesture conflicts across effective shortcut definitions.
/// </summary>
public interface IShortcutConflictDetector
{
    IReadOnlyList<ShortcutValidationIssue> DetectConflicts(IReadOnlyList<ShortcutDefinition> definitions);
}

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Result of a runtime shortcut update operation.
/// </summary>
public sealed record ShortcutUpdateResult(
    bool Success,
    ShortcutRegistrySnapshot Snapshot,
    IReadOnlyList<ShortcutValidationIssue> Issues);

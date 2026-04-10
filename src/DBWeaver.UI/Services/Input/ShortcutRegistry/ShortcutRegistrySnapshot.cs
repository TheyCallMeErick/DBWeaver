namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Immutable registry snapshot with effective definitions and validation issues.
/// </summary>
public sealed record ShortcutRegistrySnapshot(
    IReadOnlyList<ShortcutDefinition> Definitions,
    IReadOnlyList<ShortcutValidationIssue> Issues);

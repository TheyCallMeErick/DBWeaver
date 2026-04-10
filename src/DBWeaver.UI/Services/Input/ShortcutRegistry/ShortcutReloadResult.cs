namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Result of reloading the shortcut registry state.
/// </summary>
public sealed record ShortcutReloadResult(
    bool Success,
    ShortcutRegistrySnapshot Snapshot);

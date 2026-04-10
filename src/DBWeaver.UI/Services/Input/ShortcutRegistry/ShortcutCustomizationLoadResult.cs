namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Result of applying customization entries over the default shortcut catalog.
/// </summary>
public sealed record ShortcutCustomizationLoadResult(
    ShortcutRegistrySnapshot Snapshot,
    IReadOnlyList<ShortcutCustomizationEntry> AppliedOverrides);

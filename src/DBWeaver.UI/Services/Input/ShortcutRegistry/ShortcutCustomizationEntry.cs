namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Persisted customization entry for a shortcut action.
/// </summary>
public sealed record ShortcutCustomizationEntry(
    string ActionId,
    string? Gesture);

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Canonical shortcut definition consumed by input, command palette and shortcut UIs.
/// </summary>
public sealed record ShortcutDefinition(
    ShortcutActionId ActionId,
    string Name,
    string Description,
    string Section,
    IReadOnlyList<string> Tags,
    ShortcutGesture? DefaultGesture,
    ShortcutGesture? EffectiveGesture,
    ShortcutContext Context,
    bool AllowCustomization,
    Action Execute);

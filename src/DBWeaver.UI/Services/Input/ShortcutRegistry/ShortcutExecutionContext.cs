using Avalonia.Input;

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Runtime key press context used by shortcut execution.
/// </summary>
public sealed record ShortcutExecutionContext(
    Key Key,
    KeyModifiers Modifiers,
    ShortcutContext PreferredContext,
    bool AllowScopedShortcuts);

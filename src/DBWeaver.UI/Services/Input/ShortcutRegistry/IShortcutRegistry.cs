using Avalonia.Input;

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Central registry for effective keyboard shortcuts.
/// </summary>
public interface IShortcutRegistry
{
    ShortcutRegistrySnapshot GetSnapshot();
    IReadOnlyList<ShortcutDefinition> GetAll();
    ShortcutDefinition? FindByActionId(string actionId);
    ShortcutDefinition? FindByGesture(Key key, KeyModifiers modifiers, ShortcutContext context);
    ShortcutReloadResult Reload();
    ShortcutUpdateResult TryOverride(string actionId, string? gestureText);
    ShortcutUpdateResult ResetToDefault(string actionId);
    ShortcutUpdateResult ResetAllToDefault();
}

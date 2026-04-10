namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Persists shortcut customization overrides.
/// </summary>
public interface IShortcutCustomizationStore
{
    IReadOnlyList<ShortcutCustomizationEntry> LoadOverrides();
    bool TrySaveOverrides(IReadOnlyList<ShortcutCustomizationEntry> overrides);
}

using DBWeaver.UI.Services.Settings;

namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Shortcut customization store backed by <see cref="AppSettingsStore" />.
/// </summary>
public sealed class AppSettingsShortcutCustomizationStore : IShortcutCustomizationStore
{
    public IReadOnlyList<ShortcutCustomizationEntry> LoadOverrides()
    {
        AppSettings settings = AppSettingsStore.Load();
        if (settings.Shortcuts.Overrides.Count == 0)
            return [];

        return settings.Shortcuts.Overrides
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ActionId))
            .Select(entry => new ShortcutCustomizationEntry(entry.ActionId.Trim(), entry.Gesture))
            .ToList();
    }

    public bool TrySaveOverrides(IReadOnlyList<ShortcutCustomizationEntry> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        AppSettings settings = AppSettingsStore.Load();
        settings.Shortcuts.Version = 1;
        settings.Shortcuts.Overrides = overrides
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ActionId))
            .Select(entry => new ShortcutCustomizationEntry(entry.ActionId.Trim(), entry.Gesture))
            .ToList();

        return AppSettingsStore.TrySave(settings);
    }
}

using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.UI.Services.Settings;

/// <summary>
/// Persisted shortcut customization section inside app settings JSON.
/// </summary>
public sealed class ShortcutSettingsSection
{
    public int Version { get; set; } = 1;
    public List<ShortcutCustomizationEntry> Overrides { get; set; } = [];
}

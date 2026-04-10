namespace DBWeaver.UI.Services.Input.ShortcutRegistry;

/// <summary>
/// Volatile customization store used in ephemeral contexts such as tests.
/// </summary>
public sealed class NoOpShortcutCustomizationStore : IShortcutCustomizationStore
{
    public IReadOnlyList<ShortcutCustomizationEntry> LoadOverrides() => [];

    public bool TrySaveOverrides(IReadOnlyList<ShortcutCustomizationEntry> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        return true;
    }
}

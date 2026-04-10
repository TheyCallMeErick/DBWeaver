using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistrySubsystem;

public sealed class ShortcutRegistryFallbackTests
{
    [Fact]
    public void Reload_InvalidOverride_ReportsIssueAndKeepsDefaultGesture()
    {
        var store = new InMemoryShortcutCustomizationStore(
        [
            new ShortcutCustomizationEntry(ShortcutActionIds.OpenCommandPalette, "Ctrl+UnknownKey"),
        ]);

        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: store);

        ShortcutReloadResult result = registry.Reload();
        ShortcutDefinition? definition = registry.FindByActionId(ShortcutActionIds.OpenCommandPalette);

        Assert.True(result.Success);
        Assert.NotNull(definition);
        Assert.Contains(result.Snapshot.Issues, issue => issue.Code == ShortcutValidationCodes.UnknownKey);
        Assert.Equal(definition!.DefaultGesture!.NormalizedText, definition.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void TryOverride_Conflict_DoesNotCorruptExistingEffectiveGesture()
    {
        var store = new InMemoryShortcutCustomizationStore();
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: store);

        ShortcutDefinition? openPalette = registry.FindByActionId(ShortcutActionIds.OpenCommandPalette);
        Assert.NotNull(openPalette);
        string originalGesture = openPalette.EffectiveGesture!.NormalizedText;

        ShortcutUpdateResult result = registry.TryOverride(
            ShortcutActionIds.OpenCommandPalette,
            registry.FindByActionId(ShortcutActionIds.Save)!.EffectiveGesture!.NormalizedText);

        ShortcutDefinition? updated = registry.FindByActionId(ShortcutActionIds.OpenCommandPalette);

        Assert.False(result.Success);
        Assert.NotNull(updated);
        Assert.Contains(result.Issues, issue => issue.Code == ShortcutValidationCodes.DuplicateGesture);
        Assert.Equal(originalGesture, updated!.EffectiveGesture!.NormalizedText);
    }

    private sealed class InMemoryShortcutCustomizationStore : IShortcutCustomizationStore
    {
        private readonly List<ShortcutCustomizationEntry> _entries;

        public InMemoryShortcutCustomizationStore(IReadOnlyList<ShortcutCustomizationEntry>? entries = null)
        {
            _entries = entries?.ToList() ?? [];
        }

        public IReadOnlyList<ShortcutCustomizationEntry> LoadOverrides() => _entries.ToList();

        public bool TrySaveOverrides(IReadOnlyList<ShortcutCustomizationEntry> overrides)
        {
            _entries.Clear();
            _entries.AddRange(overrides);
            return true;
        }
    }
}

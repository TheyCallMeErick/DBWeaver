using Avalonia.Input;
using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistrySubsystem;

public sealed class ShortcutRegistryTests
{
    private readonly InMemoryShortcutCustomizationStore _store = new();

    [Fact]
    public void FindByActionId_ReturnsKnownDefinition()
    {
        var registry = CreateRegistry();

        ShortcutDefinition? definition = registry.FindByActionId(ShortcutActionIds.Save);

        Assert.NotNull(definition);
        Assert.Equal(ShortcutActionIds.Save, definition!.ActionId.Value);
    }

    [Fact]
    public void FindByGesture_ReturnsDefinitionForContext()
    {
        var registry = CreateRegistry();

        ShortcutDefinition? definition = registry.FindByGesture(Key.F4, KeyModifiers.None, ShortcutContext.Canvas);

        Assert.NotNull(definition);
        Assert.Equal(ShortcutActionIds.ExplainPlan, definition!.ActionId.Value);
    }

    [Fact]
    public void FindByGesture_FallsBackToGlobalWhenNoScopedDefinitionExists()
    {
        var registry = CreateRegistry();

        ShortcutDefinition? definition = registry.FindByGesture(Key.F1, KeyModifiers.None, ShortcutContext.SqlEditor);

        Assert.NotNull(definition);
        Assert.Equal(ShortcutActionIds.OpenShortcutsReference, definition!.ActionId.Value);
    }

    [Fact]
    public void TryOverride_WithValidGesture_UpdatesEffectiveGesture()
    {
        var registry = CreateRegistry();

        ShortcutUpdateResult result = registry.TryOverride(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K");
        ShortcutDefinition? updated = registry.FindByActionId(ShortcutActionIds.OpenCommandPalette);

        Assert.True(result.Success);
        Assert.NotNull(updated);
        Assert.Equal("Ctrl+Shift+K", updated!.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void ResetToDefault_RestoresDefaultGesture()
    {
        var registry = CreateRegistry();
        _ = registry.TryOverride(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K");

        ShortcutUpdateResult result = registry.ResetToDefault(ShortcutActionIds.OpenCommandPalette);
        ShortcutDefinition? updated = registry.FindByActionId(ShortcutActionIds.OpenCommandPalette);

        Assert.True(result.Success);
        Assert.NotNull(updated);
        Assert.Equal(updated!.DefaultGesture!.NormalizedText, updated.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void Reload_AppliesOverridesLoadedFromStore()
    {
        _store.Seed([new ShortcutCustomizationEntry(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K")]);
        var registry = CreateRegistry();

        ShortcutReloadResult result = registry.Reload();
        ShortcutDefinition? updated = registry.FindByActionId(ShortcutActionIds.OpenCommandPalette);

        Assert.True(result.Success);
        Assert.NotNull(updated);
        Assert.Equal("Ctrl+Shift+K", updated!.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void TryOverride_WhenPersistenceFails_ReturnsFailure()
    {
        _store.FailPersist = true;
        var registry = CreateRegistry();

        ShortcutUpdateResult result = registry.TryOverride(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K");

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == ShortcutValidationCodes.PersistenceFailure);
    }

    [Fact]
    public void TryOverride_UnknownAction_ReturnsUnknownActionIssue()
    {
        var registry = CreateRegistry();

        ShortcutUpdateResult result = registry.TryOverride("shortcut.unknown.action", "Ctrl+Shift+K");

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == ShortcutValidationCodes.UnknownAction);
    }

    [Fact]
    public void TryOverride_WhitespaceGesture_ResetsActionToDefault()
    {
        var registry = CreateRegistry();
        _ = registry.TryOverride(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K");

        ShortcutUpdateResult result = registry.TryOverride(ShortcutActionIds.OpenCommandPalette, "   ");
        ShortcutDefinition? updated = registry.FindByActionId(ShortcutActionIds.OpenCommandPalette);

        Assert.True(result.Success);
        Assert.NotNull(updated);
        Assert.Equal(updated!.DefaultGesture!.NormalizedText, updated.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void ResetAllToDefault_WhenPersistenceFails_ReturnsFailure()
    {
        var registry = CreateRegistry();
        _ = registry.TryOverride(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K");
        _store.FailPersist = true;

        ShortcutUpdateResult result = registry.ResetAllToDefault();

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == ShortcutValidationCodes.PersistenceFailure);
    }

    private global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry CreateRegistry() =>
        new(
            customizationStore: _store);

    private sealed class InMemoryShortcutCustomizationStore : IShortcutCustomizationStore
    {
        private List<ShortcutCustomizationEntry> _overrides = [];

        public bool FailPersist { get; set; }

        public IReadOnlyList<ShortcutCustomizationEntry> LoadOverrides() => _overrides.ToList();

        public bool TrySaveOverrides(IReadOnlyList<ShortcutCustomizationEntry> overrides)
        {
            if (FailPersist)
                return false;

            _overrides = overrides.ToList();
            return true;
        }

        public void Seed(IReadOnlyList<ShortcutCustomizationEntry> overrides)
        {
            _overrides = overrides.ToList();
        }
    }
}

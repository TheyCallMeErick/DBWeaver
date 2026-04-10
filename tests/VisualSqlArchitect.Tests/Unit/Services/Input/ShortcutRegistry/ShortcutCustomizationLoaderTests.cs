using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistry;

public sealed class ShortcutCustomizationLoaderTests
{
    private readonly DefaultShortcutCatalog _catalog = new();
    private readonly ShortcutCustomizationLoader _loader = new(
        new ShortcutGestureParser(),
        new ShortcutConflictDetector());

    [Fact]
    public void ApplyOverrides_UnknownAction_AddsIssueAndKeepsDefaults()
    {
        ShortcutCustomizationLoadResult result = _loader.ApplyOverrides(
            _catalog.Build(),
            [new ShortcutCustomizationEntry("unknown.action", "Ctrl+Shift+K")]);

        Assert.Contains(result.Snapshot.Issues, issue => issue.Code == ShortcutValidationCodes.UnknownAction);

        ShortcutDefinition definition = result.Snapshot.Definitions.First(item =>
            item.ActionId.Value == ShortcutActionIds.OpenCommandPalette);
        Assert.Equal(definition.DefaultGesture!.NormalizedText, definition.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void ApplyOverrides_InvalidGesture_AddsIssueAndKeepsDefaults()
    {
        ShortcutCustomizationLoadResult result = _loader.ApplyOverrides(
            _catalog.Build(),
            [new ShortcutCustomizationEntry(ShortcutActionIds.OpenCommandPalette, "Ctrl+Nope")]);

        Assert.Contains(result.Snapshot.Issues, issue => issue.Code == ShortcutValidationCodes.UnknownKey);

        ShortcutDefinition definition = result.Snapshot.Definitions.First(item =>
            item.ActionId.Value == ShortcutActionIds.OpenCommandPalette);
        Assert.Equal(definition.DefaultGesture!.NormalizedText, definition.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void ApplyOverrides_EmptyGesture_ResetsToDefault()
    {
        ShortcutCustomizationLoadResult result = _loader.ApplyOverrides(
            _catalog.Build(),
            [new ShortcutCustomizationEntry(ShortcutActionIds.OpenCommandPalette, "   ")]);

        ShortcutDefinition definition = result.Snapshot.Definitions.First(item =>
            item.ActionId.Value == ShortcutActionIds.OpenCommandPalette);
        Assert.Equal(definition.DefaultGesture!.NormalizedText, definition.EffectiveGesture!.NormalizedText);
        Assert.Contains(result.AppliedOverrides, item =>
            item.ActionId == ShortcutActionIds.OpenCommandPalette && item.Gesture is null);
    }

    [Fact]
    public void ApplyOverrides_DuplicateOverride_AddsConflictIssue()
    {
        ShortcutCustomizationLoadResult result = _loader.ApplyOverrides(
            _catalog.Build(),
            [
                new ShortcutCustomizationEntry(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K"),
                new ShortcutCustomizationEntry(ShortcutActionIds.OpenConnectionManager, "Ctrl+Shift+K"),
            ]);

        Assert.Contains(result.Snapshot.Issues, issue => issue.Code == ShortcutValidationCodes.DuplicateGesture);

        ShortcutDefinition palette = result.Snapshot.Definitions.First(item =>
            item.ActionId.Value == ShortcutActionIds.OpenCommandPalette);
        ShortcutDefinition manager = result.Snapshot.Definitions.First(item =>
            item.ActionId.Value == ShortcutActionIds.OpenConnectionManager);
        Assert.Equal("Ctrl+Shift+K", palette.EffectiveGesture!.NormalizedText);
        Assert.Equal(manager.DefaultGesture!.NormalizedText, manager.EffectiveGesture!.NormalizedText);
    }

    [Fact]
    public void ApplyOverrides_MixedValidAndInvalid_AppliesOnlyValid()
    {
        ShortcutCustomizationLoadResult result = _loader.ApplyOverrides(
            _catalog.Build(),
            [
                new ShortcutCustomizationEntry(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K"),
                new ShortcutCustomizationEntry("unknown.action", "Ctrl+1"),
                new ShortcutCustomizationEntry(ShortcutActionIds.OpenConnectionManager, "Ctrl+Nope"),
            ]);

        ShortcutDefinition palette = result.Snapshot.Definitions.First(item =>
            item.ActionId.Value == ShortcutActionIds.OpenCommandPalette);
        ShortcutDefinition manager = result.Snapshot.Definitions.First(item =>
            item.ActionId.Value == ShortcutActionIds.OpenConnectionManager);

        Assert.Equal("Ctrl+Shift+K", palette.EffectiveGesture!.NormalizedText);
        Assert.Equal(manager.DefaultGesture!.NormalizedText, manager.EffectiveGesture!.NormalizedText);
        Assert.True(result.Snapshot.Issues.Count >= 2);
    }

    [Fact]
    public void ApplyOverrides_NotCustomizableAction_AddsIssueAndKeepsEffectiveGesture()
    {
        var parser = new ShortcutGestureParser();
        ShortcutGesture defaultGesture = parser.Parse("Ctrl+Shift+P").Gesture!;
        var definitions = new List<ShortcutDefinition>
        {
            new(
                new ShortcutActionId("test.openPalette"),
                Name: "Command Palette",
                Description: "Open command palette",
                Section: "Test",
                Tags: ["test"],
                DefaultGesture: defaultGesture,
                EffectiveGesture: defaultGesture,
                Context: ShortcutContext.Global,
                AllowCustomization: false,
                Execute: () => { }),
        };

        ShortcutCustomizationLoadResult result = _loader.ApplyOverrides(
            definitions,
            [new ShortcutCustomizationEntry("test.openPalette", "Ctrl+Shift+K")]);

        ShortcutDefinition definition = Assert.Single(result.Snapshot.Definitions);
        Assert.Equal("Ctrl+Shift+P", definition.EffectiveGesture!.NormalizedText);
        Assert.Contains(result.Snapshot.Issues, issue => issue.Code == ShortcutValidationCodes.NotCustomizable);
    }

    [Fact]
    public void ApplyOverrides_WhitespaceActionId_AddsUnknownActionIssue()
    {
        ShortcutCustomizationLoadResult result = _loader.ApplyOverrides(
            _catalog.Build(),
            [new ShortcutCustomizationEntry("   ", "Ctrl+Shift+K")]);

        Assert.Contains(result.Snapshot.Issues, issue => issue.Code == ShortcutValidationCodes.UnknownAction);
    }
}

using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.ViewModels.Shortcuts;

namespace DBWeaver.Tests.Unit.ViewModels.Shortcuts;

public sealed class KeyboardShortcutsViewModelTests
{
    [Fact]
    public void SetSearchText_FiltersByGesture()
    {
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());
        var viewModel = new KeyboardShortcutsViewModel(registry);

        viewModel.SetSearchText("f4");

        Assert.True(viewModel.FilteredCount > 0);
        Assert.Contains(viewModel.Sections.SelectMany(section => section.Items), item =>
            item.ActionId == ShortcutActionIds.ExplainPlan);
    }

    [Fact]
    public void ApplyOverride_UpdatesItemAsCustomized()
    {
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());
        var viewModel = new KeyboardShortcutsViewModel(registry);

        ShortcutUpdateResult result = viewModel.ApplyOverride(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K");
        ShortcutItemViewModel updated = viewModel.Sections
            .SelectMany(section => section.Items)
            .First(item => item.ActionId == ShortcutActionIds.OpenCommandPalette);

        Assert.True(result.Success);
        Assert.True(updated.IsCustomized);
        Assert.Equal("Ctrl+Shift+K", updated.EffectiveGesture);
    }

    [Fact]
    public void ResetAll_RemovesCustomization()
    {
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());
        var viewModel = new KeyboardShortcutsViewModel(registry);
        _ = viewModel.ApplyOverride(ShortcutActionIds.OpenCommandPalette, "Ctrl+Shift+K");

        ShortcutUpdateResult result = viewModel.ResetAll();
        ShortcutItemViewModel updated = viewModel.Sections
            .SelectMany(section => section.Items)
            .First(item => item.ActionId == ShortcutActionIds.OpenCommandPalette);

        Assert.True(result.Success);
        Assert.False(updated.IsCustomized);
        Assert.Equal(updated.DefaultGesture, updated.EffectiveGesture);
    }
}

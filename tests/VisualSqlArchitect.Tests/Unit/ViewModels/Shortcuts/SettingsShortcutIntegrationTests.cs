using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.ViewModels.Shortcuts;

namespace DBWeaver.Tests.Unit.ViewModels.Shortcuts;

public sealed class SettingsShortcutIntegrationTests
{
    [Fact]
    public void SharedRegistry_PropagatesOverrideBetweenSettingsAndF1ViewModels()
    {
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());

        var settingsViewModel = new KeyboardShortcutsViewModel(registry);
        var f1ViewModel = new KeyboardShortcutsViewModel(registry);

        ShortcutUpdateResult updateResult = settingsViewModel.ApplyOverride(
            ShortcutActionIds.OpenCommandPalette,
            "Ctrl+Shift+K");

        f1ViewModel.Refresh();
        ShortcutItemViewModel f1Item = f1ViewModel.Sections
            .SelectMany(section => section.Items)
            .First(item => item.ActionId == ShortcutActionIds.OpenCommandPalette);

        Assert.True(updateResult.Success);
        Assert.Equal("Ctrl+Shift+K", f1Item.EffectiveGesture);
        Assert.True(f1Item.IsCustomized);
    }

    [Fact]
    public void ResetAll_UsingOneViewModel_UpdatesOtherAfterRefresh()
    {
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());

        var settingsViewModel = new KeyboardShortcutsViewModel(registry);
        var f1ViewModel = new KeyboardShortcutsViewModel(registry);

        _ = settingsViewModel.ApplyOverride(ShortcutActionIds.TogglePreview, "Ctrl+Shift+P");
        ShortcutUpdateResult resetResult = f1ViewModel.ResetAll();

        settingsViewModel.Refresh();
        ShortcutItemViewModel settingsItem = settingsViewModel.Sections
            .SelectMany(section => section.Items)
            .First(item => item.ActionId == ShortcutActionIds.TogglePreview);

        Assert.True(resetResult.Success);
        Assert.False(settingsItem.IsCustomized);
        Assert.Equal(settingsItem.DefaultGesture, settingsItem.EffectiveGesture);
    }
}

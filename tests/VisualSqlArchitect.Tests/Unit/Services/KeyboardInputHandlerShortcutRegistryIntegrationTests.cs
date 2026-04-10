using Avalonia.Input;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services;

public sealed class KeyboardInputHandlerShortcutRegistryIntegrationTests
{
    [Fact]
    public void ShortcutRegistryOverride_TogglePreview_ExecutesWithEffectiveGesture()
    {
        var canvas = new CanvasViewModel();
        var registry = CreateRegistry();
        ShortcutUpdateResult update = registry.TryOverride(ShortcutActionIds.TogglePreview, "Ctrl+Alt+P");
        Assert.True(update.Success);

        var sut = new KeyboardInputHandler(canvas, shortcutRegistry: registry);

        bool handled = sut.HandleShortcut(Key.P, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.True(handled);
        Assert.True(canvas.DataPreview.IsVisible);
    }

    [Fact]
    public void ShortcutRegistryOverride_TogglePreview_RespectsCanvasBlockedState()
    {
        var canvas = new CanvasViewModel();
        canvas.ConnectionManager.Open();
        var registry = CreateRegistry();
        ShortcutUpdateResult update = registry.TryOverride(ShortcutActionIds.TogglePreview, "Ctrl+Alt+P");
        Assert.True(update.Success);

        var sut = new KeyboardInputHandler(canvas, shortcutRegistry: registry);

        bool handled = sut.HandleShortcut(Key.P, KeyModifiers.Control | KeyModifiers.Alt);

        Assert.False(handled);
        Assert.False(canvas.DataPreview.IsVisible);
    }

    [Fact]
    public void Escape_ClosesCommandPaletteOverlay_WhenRegistryIsActive()
    {
        var canvas = new CanvasViewModel();
        var commandPalette = new CommandPaletteViewModel();
        commandPalette.Open();
        Assert.True(commandPalette.IsVisible);

        var sut = new KeyboardInputHandler(canvas, commandPalette, shortcutRegistry: CreateRegistry());

        bool handled = sut.HandleShortcut(Key.Escape, KeyModifiers.None);

        Assert.True(handled);
        Assert.False(commandPalette.IsVisible);
    }

    private static global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry CreateRegistry() =>
        new(customizationStore: new NoOpShortcutCustomizationStore());
}


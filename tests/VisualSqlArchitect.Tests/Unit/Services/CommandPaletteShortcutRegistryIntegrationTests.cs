using System.Runtime.Serialization;
using Avalonia.Controls;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.CommandPalette;
using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services;

public sealed class CommandPaletteShortcutRegistryIntegrationTests
{
    [Fact]
    public void TogglePreviewCommand_UsesEffectiveGestureFromRegistry()
    {
        var fixture = CreateFixture();
        ShortcutUpdateResult update = fixture.Registry.TryOverride(ShortcutActionIds.TogglePreview, "Ctrl+Alt+P");
        Assert.True(update.Success);

        fixture.Service.Refresh();
        fixture.Service.ViewModel.Open();

        PaletteCommandItem command = Assert.Single(
            fixture.Service.ViewModel.Results,
            item => item.ActionId == ShortcutActionIds.TogglePreview);
        Assert.Equal("Ctrl+Alt+P", command.Shortcut);
    }

    [Fact]
    public void Refresh_AfterOverride_UpdatesDisplayedShortcut()
    {
        var fixture = CreateFixture();
        ShortcutUpdateResult update = fixture.Registry.TryOverride(ShortcutActionIds.RunPreview, "Ctrl+Alt+R");
        Assert.True(update.Success);

        fixture.Service.Refresh();
        fixture.Service.ViewModel.Open();

        PaletteCommandItem command = Assert.Single(
            fixture.Service.ViewModel.Results,
            item => item.ActionId == ShortcutActionIds.RunPreview);
        Assert.Equal("Ctrl+Alt+R", command.Shortcut);
    }

    [Fact]
    public void FuzzyFilter_UsesEffectiveShortcutText()
    {
        var fixture = CreateFixture();
        ShortcutUpdateResult update = fixture.Registry.TryOverride(ShortcutActionIds.TogglePreview, "Ctrl+Alt+P");
        Assert.True(update.Success);

        fixture.Service.Refresh();
        fixture.Service.ViewModel.Open();
        fixture.Service.ViewModel.Query = "ctrl alt p";

        PaletteCommandItem command = Assert.Single(
            fixture.Service.ViewModel.Results,
            item => item.ActionId == ShortcutActionIds.TogglePreview);
        Assert.Equal("Ctrl+Alt+P", command.Shortcut);
    }

    private static Fixture CreateFixture()
    {
#pragma warning disable SYSLIB0050
        var window = (Window)FormatterServices.GetUninitializedObject(typeof(Window));
#pragma warning restore SYSLIB0050
        var vm = new CanvasViewModel();
        var shell = new ShellViewModel(vm, connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        var fileOps = new FileOperationsService(window, vm);
        var export = new ExportService(window, vm);
        var preview = new PreviewService(window, vm);
        var registry = new global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry(
            customizationStore: new NoOpShortcutCustomizationStore());

        var factory = new CommandPaletteFactory(
            window,
            () => shell.ActiveCanvas ?? vm,
            () => shell,
            fileOps,
            export,
            preview,
            shortcutRegistry: registry);

        return new Fixture(registry, new CommandPaletteService(factory));
    }

    private sealed record Fixture(
        global::DBWeaver.UI.Services.Input.ShortcutRegistry.ShortcutRegistry Registry,
        CommandPaletteService Service);
}


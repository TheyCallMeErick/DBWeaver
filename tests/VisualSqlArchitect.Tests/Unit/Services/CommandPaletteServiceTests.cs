using System.Runtime.Serialization;
using Avalonia.Controls;
using DBWeaver.UI.Services;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services;

public class CommandPaletteServiceTests
{
    [Fact]
    public void Refresh_ReplacesExistingCommandsWithFactoryOutput()
    {
#pragma warning disable SYSLIB0050
        var window = (Window)FormatterServices.GetUninitializedObject(typeof(Window));
#pragma warning restore SYSLIB0050
        var canvas = new CanvasViewModel();
        var shell = new ShellViewModel(canvas, connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        var fileOps = new FileOperationsService(window, canvas);
        var export = new ExportService(window, canvas);
        var preview = new PreviewService(window, canvas);

        var factory = new CommandPaletteFactory(
            window,
            () => shell.ActiveCanvas ?? canvas,
            () => shell,
            fileOps,
            export,
            preview
        );

        var vm = new CommandPaletteViewModel();
        vm.RegisterCommands(
            [
                new PaletteCommandItem
                {
                    Name = "Legacy command",
                    Description = "Should be replaced on refresh",
                    Shortcut = "L",
                    Execute = () => { },
                },
            ]
        );

        vm.Open();
        Assert.Single(vm.Results);

        var service = new CommandPaletteService(factory, vm);
        service.Refresh();
        service.ViewModel.Open();

        Assert.DoesNotContain(service.ViewModel.Results, c => c.Name == "Legacy command");
        Assert.True(service.ViewModel.Results.Count > 20);
        Assert.Contains(service.ViewModel.Results, c => c.Shortcut == "Ctrl+N");
    }
}

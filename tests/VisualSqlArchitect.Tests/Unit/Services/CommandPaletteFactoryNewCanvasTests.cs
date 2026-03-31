using Avalonia.Controls;
using System.Runtime.Serialization;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class CommandPaletteFactoryNewCanvasTests
{
    [Fact]
    public void NewCanvasCommand_UsesInjectedCreateCanvasAction()
    {
#pragma warning disable SYSLIB0050
        var window = (Window)FormatterServices.GetUninitializedObject(typeof(Window));
#pragma warning restore SYSLIB0050
        var vm = new CanvasViewModel();
        var fileOps = new FileOperationsService(window, vm);
        var export = new ExportService(window, vm);
        var preview = new PreviewService(window, vm);

        bool invoked = false;
        var factory = new CommandPaletteFactory(
            window,
            vm,
            fileOps,
            export,
            preview,
            () => invoked = true);

        factory.RegisterAllCommands();

        vm.CommandPalette.Open();
        vm.CommandPalette.Query = "new canvas";

        var cmd = Assert.Single(vm.CommandPalette.Results, r => r.Name == "New Canvas");
        cmd.Execute();

        Assert.True(invoked);
    }
}

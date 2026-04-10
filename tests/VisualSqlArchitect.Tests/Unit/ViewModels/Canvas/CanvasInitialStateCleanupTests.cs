using Avalonia;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasInitialStateCleanupTests
{
    [Fact]
    public void Constructor_DoesNotPreloadTableResultsInSearchMenu()
    {
        var vm = new CanvasViewModel();

        vm.SearchMenu.Open(new Point(0, 0));

        Assert.DoesNotContain(vm.SearchMenu.Results, result => result.IsTable);
    }

    [Fact]
    public void SetDatabaseAndResetCanvas_WithNullMetadata_LeavesCanvasEmpty()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        Assert.NotEmpty(vm.Nodes);

        vm.SetDatabaseAndResetCanvas(metadata: null);

        Assert.Empty(vm.Nodes);
        Assert.Empty(vm.Connections);
    }
}

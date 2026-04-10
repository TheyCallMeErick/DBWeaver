using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class CanvasViewModelConnectionDeletionDirtyTests
{
    [Fact]
    public void DeleteConnection_MarksCanvasAsDirty()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        var conn = vm.Connections.First(c => c.ToPin is not null);

        vm.IsDirty = false;
        vm.DeleteConnection(conn);

        Assert.True(vm.IsDirty);
        Assert.DoesNotContain(conn, vm.Connections);
    }
}



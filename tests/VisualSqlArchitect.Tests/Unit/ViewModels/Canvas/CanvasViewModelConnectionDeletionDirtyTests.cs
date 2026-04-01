using Avalonia;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class CanvasViewModelConnectionDeletionDirtyTests
{
    [Fact]
    public void DeleteConnection_MarksCanvasAsDirty()
    {
        var vm = new CanvasViewModel();
        var conn = vm.Connections.First(c => c.ToPin is not null);

        vm.IsDirty = false;
        vm.DeleteConnection(conn);

        Assert.True(vm.IsDirty);
        Assert.DoesNotContain(conn, vm.Connections);
    }
}

using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class NodeManagerDeletionBehaviorTests
{
    [Fact]
    public void DeleteSelected_RemovesSelectedNodesAndAttachedConnections()
    {
        var vm = new CanvasViewModel();
        var selected = vm.Nodes.First();
        selected.IsSelected = true;
        int beforeConnections = vm.Connections.Count;

        int attachedConnections = vm.Connections.Count(c =>
            ReferenceEquals(c.FromPin.Owner, selected) || (c.ToPin is not null && ReferenceEquals(c.ToPin.Owner, selected)));

        vm.DeleteSelected();

        Assert.DoesNotContain(selected, vm.Nodes);
        Assert.Equal(beforeConnections - attachedConnections, vm.Connections.Count);
    }

    [Fact]
    public void CleanupOrphans_RemovesOrphanNodesAndAttachedConnections()
    {
        var vm = new CanvasViewModel();
        var orphan = vm.Nodes.First();
        orphan.IsOrphan = true;
        int beforeConnections = vm.Connections.Count;

        int attachedConnections = vm.Connections.Count(c =>
            ReferenceEquals(c.FromPin.Owner, orphan) || (c.ToPin is not null && ReferenceEquals(c.ToPin.Owner, orphan)));

        vm.CleanupOrphans();

        Assert.DoesNotContain(orphan, vm.Nodes);
        Assert.Equal(beforeConnections - attachedConnections, vm.Connections.Count);
    }
}

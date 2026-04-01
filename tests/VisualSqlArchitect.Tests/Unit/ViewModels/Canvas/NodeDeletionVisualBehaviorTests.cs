using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class NodeDeletionVisualBehaviorTests
{
    [Fact]
    public void DeleteSelected_WithMultipleNodes_RemovesNodesAndAttachedWiresOnly()
    {
        var vm = new CanvasViewModel();

        Assert.True(vm.Nodes.Count >= 3);
        Assert.True(vm.Connections.Count >= 3);

        NodeViewModel n1 = vm.Nodes[0];
        NodeViewModel n2 = vm.Nodes[1];
        n1.IsSelected = true;
        n2.IsSelected = true;

        int beforeNodeCount = vm.Nodes.Count;
        int beforeConnCount = vm.Connections.Count;

        int attached = vm.Connections.Count(c =>
            ReferenceEquals(c.FromPin.Owner, n1)
            || ReferenceEquals(c.FromPin.Owner, n2)
            || (c.ToPin is not null && (ReferenceEquals(c.ToPin.Owner, n1) || ReferenceEquals(c.ToPin.Owner, n2))));

        vm.DeleteSelected();

        Assert.Equal(beforeNodeCount - 2, vm.Nodes.Count);
        Assert.Equal(beforeConnCount - attached, vm.Connections.Count);
        Assert.DoesNotContain(n1, vm.Nodes);
        Assert.DoesNotContain(n2, vm.Nodes);
    }

    [Fact]
    public void DeleteSelected_WhenNothingSelected_IsNoOp()
    {
        var vm = new CanvasViewModel();

        int beforeNodeCount = vm.Nodes.Count;
        int beforeConnCount = vm.Connections.Count;

        vm.DeleteSelected();

        Assert.Equal(beforeNodeCount, vm.Nodes.Count);
        Assert.Equal(beforeConnCount, vm.Connections.Count);
    }
}

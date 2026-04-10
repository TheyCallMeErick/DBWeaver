using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class InfiniteCanvasToolbarRoutingActionsTests
{
    [Fact]
    public void TryApplyToolbarRoutingAction_SetStraight_ChangesRoutingMode()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);

        bool changed = InfiniteCanvas.TryApplyToolbarRoutingAction(
            vm,
            new BezierWireLayer.WireToolbarHit(wire, BezierWireLayer.WireToolbarAction.SetStraight));

        Assert.True(changed);
        Assert.Equal(CanvasWireRoutingMode.Straight, wire.RoutingMode);
        Assert.Same(wire, vm.SelectedConnection);
        Assert.Equal(CanvasWireCurveMode.Straight, vm.PropertyPanel.SelectedWireCurveMode);
    }

    [Fact]
    public void TryApplyToolbarRoutingAction_SetOrthogonal_ChangesRoutingMode()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);

        bool changed = InfiniteCanvas.TryApplyToolbarRoutingAction(
            vm,
            new BezierWireLayer.WireToolbarHit(wire, BezierWireLayer.WireToolbarAction.SetOrthogonal));

        Assert.True(changed);
        Assert.Equal(CanvasWireRoutingMode.Orthogonal, wire.RoutingMode);
        Assert.Same(wire, vm.SelectedConnection);
        Assert.Equal(CanvasWireCurveMode.Orthogonal, vm.PropertyPanel.SelectedWireCurveMode);
    }

    [Fact]
    public void TryApplyToolbarRoutingAction_ReturnsFalse_ForDeleteAction()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);

        bool changed = InfiniteCanvas.TryApplyToolbarRoutingAction(
            vm,
            new BezierWireLayer.WireToolbarHit(wire, BezierWireLayer.WireToolbarAction.Delete));

        Assert.False(changed);
        Assert.Same(wire, vm.SelectedConnection);
    }
}

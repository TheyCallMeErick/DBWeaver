using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public sealed class WireRoutingModeEditingTests
{
    [Fact]
    public void SetSelectedConnectionRoutingMode_ChangesSelectedWire_AndSupportsUndoRedo()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        vm.SelectConnection(wire);

        bool changed = vm.SetSelectedConnectionRoutingMode(CanvasWireRoutingMode.Orthogonal);

        Assert.True(changed);
        Assert.Equal(CanvasWireRoutingMode.Orthogonal, wire.RoutingMode);

        vm.UndoRedo.Undo();
        Assert.Equal(CanvasWireRoutingMode.Bezier, wire.RoutingMode);

        vm.UndoRedo.Redo();
        Assert.Equal(CanvasWireRoutingMode.Orthogonal, wire.RoutingMode);
    }

    [Fact]
    public void PropertyPanelWireStyle_UpdatesSelectedWireWithoutChangingGlobalDefault()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        vm.WireCurveMode = CanvasWireCurveMode.Bezier;
        vm.SelectConnection(wire);

        vm.PropertyPanel.SelectedWireCurveMode = CanvasWireCurveMode.Straight;

        Assert.Equal(CanvasWireRoutingMode.Straight, wire.RoutingMode);
        Assert.Equal(CanvasWireCurveMode.Bezier, vm.WireCurveMode);
    }

    [Fact]
    public void PropertyPanelWireStyle_ChangesGlobalDefault_WhenNoSelectedWire()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();

        vm.PropertyPanel.SelectedWireCurveMode = CanvasWireCurveMode.Orthogonal;

        Assert.Equal(CanvasWireCurveMode.Orthogonal, vm.WireCurveMode);
    }

    [Fact]
    public void SetConnectionRoutingMode_PreservesBreakpoints_WhenSwitchingAwayAndBackToOrthogonal()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        wire.SetBreakpoints(
        [
            new WireBreakpoint(new Avalonia.Point(210, 170)),
            new WireBreakpoint(new Avalonia.Point(280, 210)),
        ]);

        Assert.True(vm.SetConnectionRoutingMode(wire, CanvasWireRoutingMode.Bezier));
        Assert.Equal(CanvasWireRoutingMode.Bezier, wire.RoutingMode);
        Assert.Equal(2, wire.Breakpoints.Count);

        Assert.True(vm.SetConnectionRoutingMode(wire, CanvasWireRoutingMode.Orthogonal));
        Assert.Equal(CanvasWireRoutingMode.Orthogonal, wire.RoutingMode);
        Assert.Equal(2, wire.Breakpoints.Count);
        Assert.Equal(new Avalonia.Point(210, 170), wire.Breakpoints[0].Position);
        Assert.Equal(new Avalonia.Point(280, 210), wire.Breakpoints[1].Position);
    }

    [Fact]
    public void SetConnectionRoutingMode_ClearsSelectedBreakpoint_WhenLeavingOrthogonal()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        wire.SetBreakpoints([new WireBreakpoint(new Avalonia.Point(220, 180))]);
        vm.SelectWireBreakpoint(wire, 0);

        Assert.True(vm.SetConnectionRoutingMode(wire, CanvasWireRoutingMode.Straight));
        Assert.False(vm.HasSelectedBreakpoint);
        Assert.Null(vm.SelectedBreakpointConnection);
        Assert.Equal(-1, vm.SelectedBreakpointIndex);
    }
}

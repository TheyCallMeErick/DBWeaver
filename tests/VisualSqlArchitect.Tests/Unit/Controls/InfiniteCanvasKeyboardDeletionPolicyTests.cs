using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class InfiniteCanvasKeyboardDeletionPolicyTests
{
    [Fact]
    public void TryHandleWireDeleteShortcut_PrioritizesSelectedBreakpointDeletion()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        wire.SetBreakpoints([new WireBreakpoint(new Avalonia.Point(220, 180))]);
        vm.SelectWireBreakpoint(wire, 0);

        bool deleteWireCalled = false;
        bool syncCalled = false;

        bool handled = InfiniteCanvas.TryHandleWireDeleteShortcut(
            vm,
            hoveredWire: null,
            tryDeleteWire: _ =>
            {
                deleteWireCalled = true;
                return true;
            },
            onSelectedBreakpointDeleted: () => syncCalled = true);

        Assert.True(handled);
        Assert.True(syncCalled);
        Assert.False(deleteWireCalled);
        Assert.Empty(wire.Breakpoints);
    }

    [Fact]
    public void TryHandleWireDeleteShortcut_UsesSelectedWire_WhenNoSelectedBreakpoint()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel selectedWire = vm.Connections.First(c => c.ToPin is not null);
        ConnectionViewModel hoveredWire = vm.Connections.First(c => c.ToPin is not null && !ReferenceEquals(c, selectedWire));
        vm.SelectConnection(selectedWire);

        ConnectionViewModel? deletedWire = null;
        bool handled = InfiniteCanvas.TryHandleWireDeleteShortcut(
            vm,
            hoveredWire,
            tryDeleteWire: wire =>
            {
                deletedWire = wire;
                return true;
            });

        Assert.True(handled);
        Assert.Same(selectedWire, deletedWire);
    }

    [Fact]
    public void TryHandleWireDeleteShortcut_FallsBackToHoveredWire_WhenNoSelectedWire()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel hoveredWire = vm.Connections.First(c => c.ToPin is not null);
        vm.ClearConnectionSelection();

        ConnectionViewModel? deletedWire = null;
        bool handled = InfiniteCanvas.TryHandleWireDeleteShortcut(
            vm,
            hoveredWire,
            tryDeleteWire: wire =>
            {
                deletedWire = wire;
                return true;
            });

        Assert.True(handled);
        Assert.Same(hoveredWire, deletedWire);
    }
}


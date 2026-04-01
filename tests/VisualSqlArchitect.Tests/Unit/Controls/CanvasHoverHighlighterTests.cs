using Avalonia;
using Avalonia.Controls;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class CanvasHoverHighlighterTests
{
    [Fact]
    public void ResolveHoverTarget_PrefersConnectedPin_WhenPointerOnConnectedPin()
    {
        var vm = new CanvasViewModel();
        AssignDistinctPinPositions(vm);
        var pinDrag = new PinDragInteraction(vm, new Canvas());

        ConnectionViewModel conn = vm.Connections.First(c => c.ToPin is not null);
        conn.FromPoint = conn.FromPin.AbsolutePosition;
        conn.ToPoint = conn.ToPin!.AbsolutePosition;
        conn.FromPin.IsConnected = true;
        conn.ToPin.IsConnected = true;

        var wireLayer = new BezierWireLayer { Connections = vm.Connections.ToList() };

        (PinViewModel? pin, ConnectionViewModel? wire) =
            CanvasHoverHighlighter.ResolveHoverTarget(vm, wireLayer, pinDrag, conn.FromPin.AbsolutePosition);

        Assert.NotNull(pin);
        Assert.Same(conn.FromPin, pin);
        Assert.NotNull(wire);
    }

    [Fact]
    public void ApplyHover_WithWire_HighlightsWireAndEndpointPins()
    {
        var vm = new CanvasViewModel();
        AssignDistinctPinPositions(vm);

        ConnectionViewModel conn = vm.Connections.First(c => c.ToPin is not null);

        CanvasHoverHighlighter.ApplyHover(vm, pin: null, wire: conn);

        Assert.True(conn.IsHighlighted);
        Assert.True(conn.FromPin.IsHovered);
        Assert.True(conn.ToPin!.IsHovered);
    }

    [Fact]
    public void ClearHover_ResetsAllPinAndWireHighlights()
    {
        var vm = new CanvasViewModel();

        foreach (PinViewModel p in vm.Nodes.SelectMany(n => n.AllPins))
            p.IsHovered = true;
        foreach (ConnectionViewModel c in vm.Connections)
            c.IsHighlighted = true;

        CanvasHoverHighlighter.ClearHover(vm);

        Assert.All(vm.Nodes.SelectMany(n => n.AllPins), p => Assert.False(p.IsHovered));
        Assert.All(vm.Connections, c => Assert.False(c.IsHighlighted));
    }

    private static void AssignDistinctPinPositions(CanvasViewModel vm)
    {
        int i = 0;
        foreach (PinViewModel pin in vm.Nodes.SelectMany(n => n.AllPins))
        {
            pin.AbsolutePosition = new Point(100 + i * 20, 180 + i * 6);
            i++;
        }
    }
}

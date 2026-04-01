using Avalonia;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class BezierWireLayerHitTestingTests
{
    [Fact]
    public void HitTestWire_ReturnsConnection_WhenPointNearCurve()
    {
        var vm = new CanvasViewModel();
        AssignDistinctPinPositions(vm);

        ConnectionViewModel conn = vm.Connections.First(c => c.ToPin is not null);
        conn.FromPoint = new Point(120, 180);
        conn.ToPoint = new Point(320, 220);

        var layer = new BezierWireLayer { Connections = vm.Connections.ToList() };

        ConnectionViewModel? hit = layer.HitTestWire(new Point(220, 200), tolerance: 20);

        Assert.NotNull(hit);
    }

    [Fact]
    public void HitTestWire_ReturnsNull_WhenPointFarFromCurves()
    {
        var vm = new CanvasViewModel();
        AssignDistinctPinPositions(vm);

        ConnectionViewModel conn = vm.Connections.First(c => c.ToPin is not null);
        conn.FromPoint = new Point(120, 180);
        conn.ToPoint = new Point(320, 220);

        var layer = new BezierWireLayer { Connections = vm.Connections.ToList() };

        ConnectionViewModel? hit = layer.HitTestWire(new Point(2000, 2000), tolerance: 8);

        Assert.Null(hit);
    }

    private static void AssignDistinctPinPositions(CanvasViewModel vm)
    {
        int i = 0;
        foreach (PinViewModel pin in vm.Nodes.SelectMany(n => n.AllPins))
        {
            pin.AbsolutePosition = new Point(100 + i * 18, 140 + i * 5);
            i++;
        }
    }
}

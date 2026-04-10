using Avalonia;
using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class BezierWireLayerHitTestingTests
{
    [Fact]
    public void HitTestWire_ReturnsConnection_WhenPointNearCurve()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();

        AssignDistinctPinPositions(vm);

        ConnectionViewModel testedConn = vm.Connections.First(c => c.ToPin is not null);
        testedConn.FromPoint = new Point(120, 180);
        testedConn.ToPoint = new Point(320, 220);

        var layer = new BezierWireLayer { Connections = vm.Connections.ToList() };

        ConnectionViewModel? hit = layer.HitTestWire(new Point(220, 200), tolerance: 20);

        Assert.NotNull(hit);
    }

    [Fact]
    public void HitTestWire_ReturnsNull_WhenPointFarFromCurves()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();

        AssignDistinctPinPositions(vm);

        ConnectionViewModel testedConn = vm.Connections.First(c => c.ToPin is not null);
        testedConn.FromPoint = new Point(120, 180);
        testedConn.ToPoint = new Point(320, 220);

        var layer = new BezierWireLayer { Connections = vm.Connections.ToList() };

        ConnectionViewModel? hit = layer.HitTestWire(new Point(2000, 2000), tolerance: 8);

        Assert.Null(hit);
    }

    [Fact]
    public void HitTestWire_UsesConnectionRoutingMode_WhenGlobalCurveModeDiffers()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();

        AssignDistinctPinPositions(vm);

        ConnectionViewModel testedConn = vm.Connections.First(c => c.ToPin is not null);
        testedConn.RoutingMode = CanvasWireRoutingMode.Straight;
        testedConn.FromPoint = new Point(120, 180);
        testedConn.ToPoint = new Point(320, 220);

        var layer = new BezierWireLayer
        {
            WireCurveMode = CanvasWireCurveMode.Bezier,
            Connections = vm.Connections.ToList(),
        };

        ConnectionViewModel? hit = layer.HitTestWire(new Point(220, 200), tolerance: 20);

        Assert.NotNull(hit);
        Assert.Same(testedConn, hit);
    }

    [Fact]
    public void HitTestWire_OrthogonalRouting_HitsOnMiddleSegments()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();

        AssignDistinctPinPositions(vm);

        ConnectionViewModel testedConn = vm.Connections.First(c => c.ToPin is not null);
        testedConn.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        testedConn.FromPoint = new Point(120, 180);
        testedConn.ToPoint = new Point(320, 260);

        var layer = new BezierWireLayer { Connections = vm.Connections.ToList() };

        ConnectionViewModel? hit = layer.HitTestWire(new Point(220, 180), tolerance: 8);

        Assert.NotNull(hit);
        Assert.Same(testedConn, hit);
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

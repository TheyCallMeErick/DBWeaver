using Avalonia;
using DBWeaver.UI.Controls;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class WireOrthogonalBreakpointGeometryTests
{
    [Fact]
    public void BuildOrthogonalPolyline_UsesExplicitBreakpoints_WhenProvided()
    {
        IReadOnlyList<Point> points = BezierWireLayer.BuildOrthogonalPolyline(
            new Point(100, 100),
            new Point(300, 220),
            [new WireBreakpoint(new Point(180, 100)), new WireBreakpoint(new Point(180, 220))]);

        Assert.Equal(4, points.Count);
        Assert.Equal(new Point(100, 100), points[0]);
        Assert.Equal(new Point(180, 100), points[1]);
        Assert.Equal(new Point(180, 220), points[2]);
        Assert.Equal(new Point(300, 220), points[3]);
    }

    [Fact]
    public void TryProjectToOrthogonalSegment_ReturnsProjectedPointAndInsertIndex()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        wire.FromPoint = new Point(120, 180);
        wire.ToPoint = new Point(320, 260);

        bool ok = BezierWireLayer.TryProjectToOrthogonalSegment(
            wire,
            point: new Point(220, 180),
            tolerance: 12,
            out Point projected,
            out int insertIndex,
            out int segmentStart);

        Assert.True(ok);
        Assert.Equal(new Point(220, 180), projected);
        Assert.Equal(0, insertIndex);
        Assert.Equal(0, segmentStart);
    }

    [Fact]
    public void FindBreakpointAt_ReturnsIndex_WhenPointerNearHandle()
    {
        var vm = new CanvasViewModel();
        vm.InitializeDemoNodes();
        ConnectionViewModel wire = vm.Connections.First(c => c.ToPin is not null);
        wire.RoutingMode = CanvasWireRoutingMode.Orthogonal;
        wire.SetBreakpoints(
        [
            new WireBreakpoint(new Point(200, 180)),
            new WireBreakpoint(new Point(240, 220)),
        ]);

        int index = BezierWireLayer.FindBreakpointAt(wire, new Point(201, 181), tolerance: 6);

        Assert.Equal(0, index);
    }
}

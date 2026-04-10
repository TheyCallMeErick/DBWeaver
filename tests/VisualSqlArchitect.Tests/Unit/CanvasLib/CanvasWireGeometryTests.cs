using DBWeaver.CanvasKit;

namespace DBWeaver.Tests.Unit.CanvasLib;

public sealed class CanvasWireGeometryTests
{
    [Fact]
    public void BuildBezierPath_UsesMinimumOffset_WhenNodesAreClose()
    {
        string path = CanvasWireGeometry.BuildBezierPath(10, 20, 30, 40);

        Assert.Contains("C 70.0,20.0 -30.0,40.0", path, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBezierPath_UsesHalfDistanceOffset_WhenNodesAreFar()
    {
        string path = CanvasWireGeometry.BuildBezierPath(0, 0, 300, 100);

        Assert.Contains("C 150.0,0.0 150.0,100.0", path, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBezierPath_FormatsAsSvgCubicCurve()
    {
        string path = CanvasWireGeometry.BuildBezierPath(1, 2, 3, 4);

        Assert.StartsWith("M 1.0,2.0 C ", path, StringComparison.Ordinal);
        Assert.EndsWith("3.0,4.0", path, StringComparison.Ordinal);
    }
}

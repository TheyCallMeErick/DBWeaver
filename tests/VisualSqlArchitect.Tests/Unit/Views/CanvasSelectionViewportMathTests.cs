using Avalonia;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.Views;

public class CanvasSelectionViewportMathTests
{
    private static NodeViewModel Node(string name, double x, double y, double width = 220)
    {
        return new NodeViewModel($"public.{name}", Array.Empty<(string n, VisualSqlArchitect.Nodes.PinDataType t)>(), new Point(x, y))
        {
            Width = width,
        };
    }

    [Fact]
    public void TryGetSelectionBounds_ReturnsFalse_ForEmptySelection()
    {
        bool ok = CanvasSelectionViewportMath.TryGetSelectionBounds(
            Array.Empty<NodeViewModel>(),
            defaultNodeHeight: 130,
            out _
        );

        Assert.False(ok);
    }

    [Fact]
    public void ComputeCenterPan_CentersSelectionAtViewportMiddle()
    {
        var nodes = new[]
        {
            Node("a", 100, 200),
            Node("b", 500, 200),
        };

        bool ok = CanvasSelectionViewportMath.TryGetSelectionBounds(nodes, 130, out var bounds);
        Assert.True(ok);

        Point pan = CanvasSelectionViewportMath.ComputeCenterPan(bounds, new Size(1200, 800), zoom: 1.0);
        Point centerScreen = new(bounds.Center.X * 1.0 + pan.X, bounds.Center.Y * 1.0 + pan.Y);

        Assert.Equal(600, centerScreen.X, 4);
        Assert.Equal(400, centerScreen.Y, 4);
    }

    [Fact]
    public void ComputeFit_WorksForFarCoordinates()
    {
        var nodes = new[]
        {
            Node("a", 5000, 5000),
            Node("b", 5600, 5300),
        };

        bool ok = CanvasSelectionViewportMath.TryGetSelectionBounds(nodes, 130, out var bounds);
        Assert.True(ok);

        (double zoom, Point pan) = CanvasSelectionViewportMath.ComputeFit(bounds, new Size(1200, 800), 40, 0.15, 4.0);

        Assert.InRange(zoom, 0.15, 4.0);

        // Both node top-left points should map inside viewport after fit.
        foreach (NodeViewModel n in nodes)
        {
            Point screen = new(n.Position.X * zoom + pan.X, n.Position.Y * zoom + pan.Y);
            Assert.True(screen.X >= -2 && screen.X <= 1202);
            Assert.True(screen.Y >= -2 && screen.Y <= 802);
        }
    }

    [Fact]
    public void ComputeFit_ClampsToMaxZoom_ForTinySelection()
    {
        var nodes = new[]
        {
            Node("a", 100, 100, width: 30),
            Node("b", 110, 105, width: 30),
        };

        bool ok = CanvasSelectionViewportMath.TryGetSelectionBounds(nodes, 20, out var bounds);
        Assert.True(ok);

        (double zoom, Point _) = CanvasSelectionViewportMath.ComputeFit(bounds, new Size(1600, 900), 40, 0.15, 4.0);

        Assert.Equal(4.0, zoom, 8);
    }

    [Fact]
    public void ComputeFit_ClampsToMinZoom_ForHugeSelection()
    {
        var nodes = new[]
        {
            Node("a", -20000, -15000, width: 6000),
            Node("b", 30000, 25000, width: 7000),
        };

        bool ok = CanvasSelectionViewportMath.TryGetSelectionBounds(nodes, 4000, out var bounds);
        Assert.True(ok);

        (double zoom, Point _) = CanvasSelectionViewportMath.ComputeFit(bounds, new Size(1200, 800), 40, 0.15, 4.0);

        Assert.Equal(0.15, zoom, 8);
    }
}

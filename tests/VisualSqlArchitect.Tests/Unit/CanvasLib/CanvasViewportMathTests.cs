using DBWeaver.CanvasKit;

namespace DBWeaver.Tests.Unit.CanvasLib;

public sealed class CanvasViewportMathTests
{
    [Fact]
    public void TryGetSelectionBounds_ReturnsFalse_ForEmptySelection()
    {
        bool ok = CanvasViewportMath.TryGetSelectionBounds(Array.Empty<CanvasViewportNodeFrame>(), out _);

        Assert.False(ok);
    }

    [Fact]
    public void ComputeCenterPan_CentersSelection()
    {
        CanvasViewportNodeFrame[] nodes =
        [
            new(100, 200, 220, 130),
            new(500, 200, 220, 130),
        ];

        bool ok = CanvasViewportMath.TryGetSelectionBounds(nodes, out CanvasSelectionBounds bounds);
        Assert.True(ok);

        CanvasViewportPoint pan = CanvasViewportMath.ComputeCenterPan(bounds, new CanvasViewportSize(1200, 800), zoom: 1.0);
        CanvasViewportPoint centerScreen = new(bounds.Center.X + pan.X, bounds.Center.Y + pan.Y);

        Assert.Equal(600, centerScreen.X, 4);
        Assert.Equal(400, centerScreen.Y, 4);
    }

    [Fact]
    public void ComputeFit_ClampsToMaxZoom_ForTinySelection()
    {
        CanvasViewportNodeFrame[] nodes =
        [
            new(100, 100, 30, 20),
            new(110, 105, 30, 20),
        ];

        bool ok = CanvasViewportMath.TryGetSelectionBounds(nodes, out CanvasSelectionBounds bounds);
        Assert.True(ok);

        (double zoom, CanvasViewportPoint _) = CanvasViewportMath.ComputeFit(bounds, new CanvasViewportSize(1600, 900), 40, 0.15, 4.0);

        Assert.Equal(4.0, zoom, 8);
    }

    [Fact]
    public void ComputeFit_ClampsToMinZoom_ForHugeSelection()
    {
        CanvasViewportNodeFrame[] nodes =
        [
            new(-20000, -15000, 6000, 4000),
            new(30000, 25000, 7000, 4000),
        ];

        bool ok = CanvasViewportMath.TryGetSelectionBounds(nodes, out CanvasSelectionBounds bounds);
        Assert.True(ok);

        (double zoom, CanvasViewportPoint _) = CanvasViewportMath.ComputeFit(bounds, new CanvasViewportSize(1200, 800), 40, 0.15, 4.0);

        Assert.Equal(0.15, zoom, 8);
    }
}

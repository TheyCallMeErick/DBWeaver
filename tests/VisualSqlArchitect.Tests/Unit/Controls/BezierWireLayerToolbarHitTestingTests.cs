using Avalonia;
using DBWeaver.UI.Controls;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class BezierWireLayerToolbarHitTestingTests
{
    [Fact]
    public void BuildToolbarLayout_CreatesStableButtonRects()
    {
        BezierWireLayer.WireToolbarLayout layout = BezierWireLayer.BuildToolbarLayout(new Point(200, 200));

        Assert.Equal(new Rect(104, 162, 192, 22), layout.Toolbar);
        Assert.Equal(new Rect(110, 165, 48, 16), layout.Bezier);
        Assert.Equal(new Rect(162, 165, 48, 16), layout.Straight);
        Assert.Equal(new Rect(214, 165, 48, 16), layout.Orthogonal);
        Assert.Equal(new Rect(272, 165, 18, 16), layout.Delete);
    }

    [Fact]
    public void TryResolveToolbarAction_ReturnsExpectedAction_ForEachButton()
    {
        BezierWireLayer.WireToolbarLayout layout = BezierWireLayer.BuildToolbarLayout(new Point(200, 200));

        Assert.True(BezierWireLayer.TryResolveToolbarAction(Center(layout.Bezier), layout, out BezierWireLayer.WireToolbarAction bezier));
        Assert.Equal(BezierWireLayer.WireToolbarAction.SetBezier, bezier);

        Assert.True(BezierWireLayer.TryResolveToolbarAction(Center(layout.Straight), layout, out BezierWireLayer.WireToolbarAction straight));
        Assert.Equal(BezierWireLayer.WireToolbarAction.SetStraight, straight);

        Assert.True(BezierWireLayer.TryResolveToolbarAction(Center(layout.Orthogonal), layout, out BezierWireLayer.WireToolbarAction orthogonal));
        Assert.Equal(BezierWireLayer.WireToolbarAction.SetOrthogonal, orthogonal);

        Assert.True(BezierWireLayer.TryResolveToolbarAction(Center(layout.Delete), layout, out BezierWireLayer.WireToolbarAction delete));
        Assert.Equal(BezierWireLayer.WireToolbarAction.Delete, delete);
    }

    [Fact]
    public void TryResolveToolbarAction_ReturnsFalse_WhenPointOutsideAllButtons()
    {
        BezierWireLayer.WireToolbarLayout layout = BezierWireLayer.BuildToolbarLayout(new Point(200, 200));

        bool hit = BezierWireLayer.TryResolveToolbarAction(new Point(60, 60), layout, out _);

        Assert.False(hit);
    }

    private static Point Center(Rect rect) =>
        new(rect.X + (rect.Width * 0.5), rect.Y + (rect.Height * 0.5));
}


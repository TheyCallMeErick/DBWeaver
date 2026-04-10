using DBWeaver.CanvasKit;

namespace DBWeaver.Tests.Unit.CanvasLib;

public sealed class CanvasWireStylePolicyTests
{
    [Fact]
    public void ResolveThickness_ReturnsBase_WhenNotHighlighted()
    {
        double thickness = CanvasWireStylePolicy.ResolveThickness(2.5, isHighlighted: false);

        Assert.Equal(2.5, thickness);
    }

    [Fact]
    public void ResolveThickness_AddsBoost_WhenHighlighted()
    {
        double thickness = CanvasWireStylePolicy.ResolveThickness(2.5, isHighlighted: true);

        Assert.Equal(3.2, thickness, 6);
    }

    [Fact]
    public void ResolveThickness_UsesCustomBoost_WhenProvided()
    {
        double thickness = CanvasWireStylePolicy.ResolveThickness(1.0, isHighlighted: true, highlightBoost: 1.25);

        Assert.Equal(2.25, thickness, 6);
    }
}

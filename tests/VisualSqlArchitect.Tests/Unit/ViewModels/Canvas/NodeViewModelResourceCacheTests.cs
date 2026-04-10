using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class NodeViewModelResourceCacheTests
{
    [Fact]
    public void HeaderGradient_IsReusedAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var g1 = node.HeaderGradient;
        var g2 = node.HeaderGradient;

        Assert.Same(g1, g2);
    }

    [Fact]
    public void HeaderColor_IsStableAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var c1 = node.HeaderColor;
        var c2 = node.HeaderColor;

        Assert.Equal(c1, c2);
    }

    [Fact]
    public void HeaderColorLight_IsStableAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var c1 = node.HeaderColorLight;
        var c2 = node.HeaderColorLight;

        Assert.Equal(c1, c2);
    }

    [Fact]
    public void NodeBorderBrush_DefaultState_IsReusedAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var b1 = node.NodeBorderBrush;
        var b2 = node.NodeBorderBrush;

        Assert.Same(b1, b2);
    }

    [Fact]
    public void NodeBorderBrush_UsesHighlightBrush_WhenIsHighlighted()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        var defaultBrush = node.NodeBorderBrush;

        node.IsHighlighted = true;

        Assert.NotSame(defaultBrush, node.NodeBorderBrush);
    }
}



using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using Avalonia.Media;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PinViewModelVisualStateTests
{
    [Fact]
    public void PinFillBrush_IsSemiTransparent_WhenDisconnected()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "id");

        pin.IsConnected = false;

        var brush = Assert.IsType<SolidColorBrush>(pin.PinFillBrush);
        Assert.Equal((byte)72, brush.Color.A);
    }

    [Fact]
    public void PinFillBrush_IsOpaque_WhenConnected()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "id");

        pin.IsConnected = true;

        var brush = Assert.IsType<SolidColorBrush>(pin.PinFillBrush);
        Assert.Equal((byte)255, brush.Color.A);
    }

    [Fact]
    public void VisualOpacity_ReflectsDragCompatibility()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "id");

        pin.IsDragIncompatible = true;
        Assert.Equal(0.2, pin.VisualOpacity, 3);

        pin.IsDragIncompatible = false;
        Assert.Equal(1.0, pin.VisualOpacity, 3);
    }

    [Fact]
    public void DropTargetBrush_UsesHighlightColor_WhenDropTarget()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "id");

        pin.IsDropTarget = true;

        var brush = Assert.IsType<SolidColorBrush>(pin.DropTargetBrush);
        Assert.Equal(Color.Parse("#FACC15"), brush.Color);
    }

    [Fact]
    public void ShowOutputFallbackVisual_IsTrue_ForScalarLikePins()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "id");

        Assert.True(pin.ShowOutputFallbackVisual);
    }

    [Fact]
    public void ShowOutputFallbackVisual_IsFalse_ForStructuralPins()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        PinViewModel star = node.OutputPins.First(p => p.Name == "*");

        Assert.False(star.ShowOutputFallbackVisual);
    }

    [Fact]
    public void HasAbsolutePosition_BecomesTrue_AfterFirstAbsolutePositionUpdate()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        PinViewModel pin = node.OutputPins.First(p => p.Name == "id");

        Assert.False(pin.HasAbsolutePosition);

        pin.AbsolutePosition = new Point(120, 45);

        Assert.True(pin.HasAbsolutePosition);
    }
}



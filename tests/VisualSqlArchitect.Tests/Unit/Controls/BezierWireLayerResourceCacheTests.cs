using System.Reflection;
using Avalonia.Media;
using DBWeaver.UI.Controls;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class BezierWireLayerResourceCacheTests
{
    [Fact]
    public void GetSolidBrush_ReusesSameInstanceForSameColor()
    {
        var layer = new BezierWireLayer();
        var method = typeof(BezierWireLayer).GetMethod("GetSolidBrush", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var color = Color.Parse("#60A5FA");
        var first = method!.Invoke(layer, [color]);
        var second = method.Invoke(layer, [color]);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetDragPen_ReusesSameInstanceForSameColor()
    {
        var layer = new BezierWireLayer();
        var method = typeof(BezierWireLayer).GetMethod("GetDragPen", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var color = Color.Parse("#4ADE80");
        var first = method!.Invoke(layer, [color]);
        var second = method.Invoke(layer, [color]);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }
}

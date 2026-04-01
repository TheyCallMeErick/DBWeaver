using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class InfiniteCanvasWireLayerOrderingTests
{
    [Fact]
    public void EnsureWiresOnTop_KeepsWiresBehindNodes()
    {
        string source = ReadInfiniteCanvasSource();

        Assert.Contains("_wires.ZIndex = -10_000;", source);
        Assert.Contains("_rubberBandRect.ZIndex = -9_999;", source);
    }

    [Fact]
    public void NodeDrag_RefreshesWireLayerOrderOnStartAndEnd()
    {
        string source = ReadNodeDragSource();

        Assert.Contains("EnsureWiresOnTop();", source);
        Assert.Contains("_wires.InvalidateVisual();", source);
    }

    private static string ReadInfiniteCanvasSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "VisualSqlArchitect.UI",
                "Controls",
                "InfiniteCanvas",
                "InfiniteCanvas.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate InfiniteCanvas.cs from test base directory.");
    }

    private static string ReadNodeDragSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "VisualSqlArchitect.UI",
                "Controls",
                "InfiniteCanvas",
                "InfiniteCanvas.NodeDrag.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate InfiniteCanvas.NodeDrag.cs from test base directory.");
    }
}

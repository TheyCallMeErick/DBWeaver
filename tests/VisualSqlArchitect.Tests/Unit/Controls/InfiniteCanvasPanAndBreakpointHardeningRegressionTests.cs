using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class InfiniteCanvasPanAndBreakpointHardeningRegressionTests
{
    [Fact]
    public void OnMoved_PanPath_ResyncsWiresToKeepWireAffordancesAligned()
    {
        string source = ReadInfiniteCanvasInteractionSource();

        Assert.Contains("if (_isPanning)", source);
        Assert.Contains("SyncWires();", source);
    }

    [Fact]
    public void ContextPan_DoesNotStartWhileDraggingBreakpointHandle()
    {
        string source = ReadInfiniteCanvasInteractionSource();

        Assert.Contains("_contextMenuPending && !_isPanning && _dragBreakpointWire is null", source);
    }

    private static string ReadInfiniteCanvasInteractionSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "InfiniteCanvas",
                "InfiniteCanvas.Interaction.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate InfiniteCanvas.Interaction.cs from test base directory.");
    }
}

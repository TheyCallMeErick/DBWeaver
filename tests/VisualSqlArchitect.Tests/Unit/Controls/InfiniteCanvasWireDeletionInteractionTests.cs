using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class InfiniteCanvasWireDeletionInteractionTests
{
    [Fact]
    public void Interaction_IncludesDeleteWireHelper_AndDeleteKeyPath()
    {
        string source = ReadInteractionSource();

        Assert.Contains("private bool TryDeleteWire(ConnectionViewModel? wire)", source);
        Assert.Contains("if (e.Key is Key.Delete or Key.Back)", source);
        Assert.Contains("TryDeleteWire(_hoveredWire)", source);
        Assert.Contains("_wires.AddRemovalFlash(wire);", source);
    }

    [Fact]
    public void Interaction_SupportsCtrlClickAndContextMenuWireDeletion()
    {
        string source = ReadInteractionSource();

        Assert.Contains("if (e.KeyModifiers.HasFlag(KeyModifiers.Control))", source);
        Assert.Contains("_wires.HitTestWire(canvas, tolerance: 8)", source);
        Assert.Contains("Header = \"Delete wire\"", source);
        Assert.Contains("TryDeleteWire(wireUnderPointer)", source);
    }

    private static string ReadInteractionSource()
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
                "InfiniteCanvas.Interaction.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate InfiniteCanvas.Interaction.cs from test base directory.");
    }
}

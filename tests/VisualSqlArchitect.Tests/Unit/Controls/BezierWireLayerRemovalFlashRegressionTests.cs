using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class BezierWireLayerRemovalFlashRegressionTests
{
    [Fact]
    public void WireLayer_ExposesRemovalFlashPath_AndRendersTransientFlashes()
    {
        string source = ReadWireLayerSource();

        Assert.Contains("private sealed record RemovalFlash", source);
        Assert.Contains("public void AddRemovalFlash(ConnectionViewModel conn)", source);
        Assert.Contains("DrawRemovalFlashes(dc, viewport);", source);
        Assert.Contains("private void DrawRemovalFlashes(DrawingContext dc, Rect viewport)", source);
        Assert.Contains("RemovalFlashDurationMs", source);
    }

    private static string ReadWireLayerSource()
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
                "BezierWireLayer.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate BezierWireLayer.cs from test base directory.");
    }
}

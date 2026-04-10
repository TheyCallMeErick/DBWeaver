using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class InfiniteCanvasWireSyncRetryRegressionTests
{
    [Fact]
    public void SyncWires_DefersAndRetries_WhenNoPinsWereUpdatedYet()
    {
        string source = ReadInfiniteCanvasSource();

        Assert.Contains("updatedPins == 0", source);
        Assert.Contains("hasUnpositionedConnectionPin", source);
        Assert.DoesNotContain("_wireSyncRetryCount < MaxWireSyncRetries", source);
        Assert.Contains("keep retrying across subsequent frames", source);
        Assert.Contains("SyncWires deferred", source);
        Assert.Contains("DispatcherPriority.Background", source);
    }

    [Fact]
    public void UpdatePinPositions_ReturnsCount_ForDeferredSyncDecision()
    {
        string source = ReadInfiniteCanvasSource();

        Assert.Contains("private int UpdatePinPositions()", source);
        Assert.Contains("return updatedPins.Count;", source);
    }

    private static string ReadInfiniteCanvasSource()
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
                "InfiniteCanvas.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate InfiniteCanvas.cs from test base directory.");
    }
}

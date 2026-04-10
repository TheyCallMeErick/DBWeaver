using System.IO;
using Xunit;

namespace DBWeaver.Tests.Unit.Controls;

public class InfiniteCanvasWireSyncSchedulingRegressionTests
{
    [Fact]
    public void NodeCollectionChanged_HandlerSchedulesWireSync()
    {
        string source = ReadInfiniteCanvasSource();

        Assert.Contains("ViewModel.Nodes.CollectionChanged +=", source);
        Assert.Contains("SyncNodes();", source);
        Assert.Contains("RequestWireSync();", source);
    }

    [Fact]
    public void SyncNodes_SchedulesWireSyncAfterTopologyUpdate()
    {
        string source = ReadInfiniteCanvasSource();

        Assert.Contains("SyncNodes: Completed", source);
        Assert.Contains("EnsureWiresOnTop();", source);
        Assert.Contains("RequestWireSync();", source);
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

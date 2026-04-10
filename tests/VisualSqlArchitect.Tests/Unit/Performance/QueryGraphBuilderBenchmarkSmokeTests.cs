using Avalonia;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using Xunit;

namespace DBWeaver.Tests.Unit.Performance;

public class QueryGraphBuilderBenchmarkSmokeTests
{
    [Fact]
    public void BuildSql_OnLargeGraph_CompletesWithinBaselineBudget()
    {
        var canvas = new CanvasViewModel();

        // Inflate graph size with additional nodes to provide a baseline perf signal.
        for (int i = 0; i < 300; i++)
        {
            canvas.SpawnNode(
                NodeDefinitionRegistry.Get(NodeType.ValueNumber),
                new Point(100 + (i % 30) * 30, 400 + (i / 30) * 30)
            );
        }

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ex = Record.Exception(() => sut.BuildSql());
        sw.Stop();

        Assert.Null(ex);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Large-graph SQL build exceeded baseline budget: {sw.ElapsedMilliseconds}ms");
    }
}


using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainContractsTests
{
    [Fact]
    public void ExplainOptions_Defaults_AreStable()
    {
        var options = new ExplainOptions();

        Assert.False(options.IncludeAnalyze);
        Assert.False(options.IncludeBuffers);
        Assert.Equal(ExplainFormat.Text, options.Format);
    }

    [Fact]
    public async Task ExplainResult_CanBeReturnedFromExecutorContract()
    {
        IExplainExecutor executor = new FakeExecutor();
        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "db",
            Username: "u",
            Password: "p"
        );

        ExplainResult result = await executor.RunAsync(
            "SELECT 1",
            DatabaseProvider.Postgres,
            cfg,
            new ExplainOptions(Format: ExplainFormat.Json)
        );

        Assert.Equal("raw", result.RawOutput);
        Assert.True(result.IsSimulated);
        Assert.Single(result.Nodes);
    }

    private sealed class FakeExecutor : IExplainExecutor
    {
        public Task<ExplainResult> RunAsync(
            string sql,
            DatabaseProvider provider,
            ConnectionConfig? connectionConfig,
            ExplainOptions options,
            CancellationToken ct = default
        )
        {
            return Task.FromResult(
                new ExplainResult(
                    Nodes:
                    [
                        new ExplainNode
                        {
                            NodeType = "Plan Step",
                            Detail = sql,
                            AlertLabel = provider.ToString(),
                        },
                    ],
                    PlanningTimeMs: 0.1,
                    ExecutionTimeMs: 0.2,
                    RawOutput: "raw",
                    IsSimulated: true
                )
            );
        }
    }
}


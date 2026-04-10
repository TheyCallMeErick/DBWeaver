using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class MySqlExplainExecutorTests
{
    [Fact]
    public async Task RunAsync_Throws_WhenProviderIsNotMySql()
    {
        var sut = new MySqlExplainExecutor();
        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: "demo.db",
            Username: string.Empty,
            Password: string.Empty
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RunAsync("SELECT 1", DatabaseProvider.SQLite, cfg, new ExplainOptions())
        );
    }

    [Fact]
    public async Task RunAsync_Throws_WhenConnectionConfigMissing()
    {
        var sut = new MySqlExplainExecutor();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RunAsync("SELECT 1", DatabaseProvider.MySql, null, new ExplainOptions())
        );
    }

    [Fact]
    public async Task RunAsync_UsesAnalyze_WhenRequested()
    {
        const string analyze = "-> Table scan on orders  (cost=12.4 rows=3) (actual time=0.1..0.3 rows=3 loops=1)";
        var runner = new FakeRunner(analyzeText: analyze, jsonText: "{}");
        var sut = new MySqlExplainExecutor(runner, new MySqlExplainPlanParser());

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders",
            DatabaseProvider.MySql,
            BuildMySqlConfig(),
            new ExplainOptions(IncludeAnalyze: true)
        );

        Assert.False(result.IsSimulated);
        Assert.Equal(1, runner.AnalyzeCalls);
        Assert.Equal(0, runner.JsonCalls);
        Assert.Single(result.Nodes);
        Assert.Equal("SEQ SCAN", result.Nodes[0].AlertLabel);
        Assert.Equal(0.3, result.ExecutionTimeMs);
    }

    [Fact]
    public async Task RunAsync_FallsBackToJson_WhenAnalyzeFails()
    {
        const string json = """
            {
              "query_block": {
                "table": {
                  "table_name": "orders",
                  "access_type": "ALL",
                  "rows_examined_per_scan": 5
                }
              }
            }
            """;
        var runner = new FakeRunner(analyzeText: "unused", jsonText: json) { ThrowOnAnalyze = true };
        var sut = new MySqlExplainExecutor(runner, new MySqlExplainPlanParser());

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders",
            DatabaseProvider.MySql,
            BuildMySqlConfig(),
            new ExplainOptions(IncludeAnalyze: true)
        );

        Assert.Equal(1, runner.AnalyzeCalls);
        Assert.Equal(1, runner.JsonCalls);
        Assert.Contains(result.Nodes, n => n.AlertLabel == "SEQ SCAN");
    }

    [Fact]
    public async Task RunAsync_UsesJson_WhenAnalyzeDisabled()
    {
        const string json = """
            {
              "query_block": {
                "ordering_operation": { "using_filesort": true },
                "nested_loop": [{
                  "table": {
                    "table_name": "orders",
                    "access_type": "ALL",
                    "cost_info": { "read_cost": "10.2", "eval_cost": "1.3" },
                    "rows_examined_per_scan": 7
                  }
                }]
              }
            }
            """;
        var runner = new FakeRunner(analyzeText: "unused", jsonText: json);
        var sut = new MySqlExplainExecutor(runner, new MySqlExplainPlanParser());

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders",
            DatabaseProvider.MySql,
            BuildMySqlConfig(),
            new ExplainOptions(IncludeAnalyze: false)
        );

        Assert.Equal(0, runner.AnalyzeCalls);
        Assert.Equal(1, runner.JsonCalls);
        Assert.Contains(result.Nodes, n => n.AlertLabel == "SORT");
        Assert.Contains(result.Nodes, n => n.AlertLabel == "SEQ SCAN");
    }

    [Fact]
    public async Task RunAsync_DoesNotFallback_WhenAnalyzeIsCancelled()
    {
        var runner = new FakeRunner(analyzeText: "unused", jsonText: "{}")
        {
            ThrowCancellationOnAnalyze = true
        };
        var sut = new MySqlExplainExecutor(runner, new MySqlExplainPlanParser());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.RunAsync(
                "SELECT * FROM orders",
                DatabaseProvider.MySql,
                BuildMySqlConfig(),
                new ExplainOptions(IncludeAnalyze: true),
                ct: new CancellationToken(canceled: true))
        );

        Assert.Equal(1, runner.AnalyzeCalls);
        Assert.Equal(0, runner.JsonCalls);
    }

    private static ConnectionConfig BuildMySqlConfig() =>
        new(
            Provider: DatabaseProvider.MySql,
            Host: "localhost",
            Port: 3306,
            Database: "db",
            Username: "u",
            Password: "p"
        );

    private sealed class FakeRunner(string analyzeText, string jsonText) : IMySqlExplainQueryRunner
    {
        public bool ThrowOnAnalyze { get; init; }
        public bool ThrowCancellationOnAnalyze { get; init; }
        public int AnalyzeCalls { get; private set; }
        public int JsonCalls { get; private set; }

        public Task<string> ExecuteFormatJsonAsync(
            string sql,
            ConnectionConfig connectionConfig,
            CancellationToken ct = default
        )
        {
            JsonCalls++;
            return Task.FromResult(jsonText);
        }

        public Task<string> ExecuteAnalyzeAsync(
            string sql,
            ConnectionConfig connectionConfig,
            CancellationToken ct = default
        )
        {
            AnalyzeCalls++;
            if (ThrowCancellationOnAnalyze)
                throw new OperationCanceledException(ct);
            if (ThrowOnAnalyze)
                throw new InvalidOperationException("analyze not supported");
            return Task.FromResult(analyzeText);
        }
    }
}

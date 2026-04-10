using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PostgresExplainExecutorTests
{
    [Fact]
    public async Task RunAsync_Throws_WhenProviderIsNotPostgres()
    {
        var sut = new PostgresExplainExecutor();
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
        var sut = new PostgresExplainExecutor();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RunAsync("SELECT 1", DatabaseProvider.Postgres, null, new ExplainOptions())
        );
    }

    [Fact]
    public async Task RunAsync_UsesRunnerAndParser_ReturnsRealResult()
    {
        const string payload = """
            [{
              "Plan": { "Node Type": "Seq Scan", "Total Cost": 10.0, "Plan Rows": 5 },
              "Planning Time": 0.1,
              "Execution Time": 0.2
            }]
            """;
        var runner = new FakeRunner(payload);
        var sut = new PostgresExplainExecutor(queryRunner: runner);

        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "db",
            Username: "u",
            Password: "p"
        );

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders",
            DatabaseProvider.Postgres,
            cfg,
            new ExplainOptions(IncludeAnalyze: true, IncludeBuffers: true, Format: ExplainFormat.Json)
        );

        Assert.False(result.IsSimulated);
        Assert.Single(result.Nodes);
        Assert.Equal("SEQ SCAN", result.Nodes[0].AlertLabel);
        Assert.Contains("ANALYZE TRUE", runner.CapturedSql);
        Assert.Contains("BUFFERS TRUE", runner.CapturedSql);
    }

    [Fact]
    public void BuildExplainSql_IncludesMandatoryFormatAndOptionalFlags()
    {
        string sql = PostgresExplainExecutor.BuildExplainSql(
            "SELECT 1",
            new ExplainOptions(IncludeAnalyze: true, IncludeBuffers: true, Format: ExplainFormat.Json)
        );

        Assert.StartsWith("EXPLAIN (FORMAT JSON", sql);
        Assert.Contains("ANALYZE TRUE", sql);
        Assert.Contains("BUFFERS TRUE", sql);
    }

    [Fact]
    public void BuildExplainSql_DoesNotIncludeBuffers_WhenAnalyzeDisabled()
    {
        string sql = PostgresExplainExecutor.BuildExplainSql(
            "SELECT 1",
            new ExplainOptions(IncludeAnalyze: false, IncludeBuffers: true, Format: ExplainFormat.Json)
        );

        Assert.StartsWith("EXPLAIN (FORMAT JSON", sql);
        Assert.DoesNotContain("ANALYZE TRUE", sql);
        Assert.DoesNotContain("BUFFERS TRUE", sql);
    }

    [Theory]
    [InlineData(ExplainFormat.Text, "FORMAT TEXT")]
    [InlineData(ExplainFormat.Json, "FORMAT JSON")]
    [InlineData(ExplainFormat.Xml, "FORMAT XML")]
    public void BuildExplainSql_UsesRequestedFormat(ExplainFormat format, string expectedFragment)
    {
        string sql = PostgresExplainExecutor.BuildExplainSql(
            "SELECT 1",
            new ExplainOptions(Format: format)
        );

        Assert.Contains(expectedFragment, sql);
    }

    private sealed class FakeRunner(string payload) : IPostgresExplainQueryRunner
    {
        public string CapturedSql { get; private set; } = string.Empty;

        public Task<string> ExecuteAsync(
            string explainSql,
            ConnectionConfig connectionConfig,
            CancellationToken ct = default
        )
        {
            CapturedSql = explainSql;
            return Task.FromResult(payload);
        }
    }
}


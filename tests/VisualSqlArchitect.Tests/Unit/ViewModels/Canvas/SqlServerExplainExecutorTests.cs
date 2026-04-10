using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlServerExplainExecutorTests
{
    [Fact]
    public async Task RunAsync_Throws_WhenProviderIsNotSqlServer()
    {
        var sut = new SqlServerExplainExecutor();
        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "demo",
            Username: "u",
            Password: "p"
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RunAsync("SELECT 1", DatabaseProvider.Postgres, cfg, new ExplainOptions())
        );
    }

    [Fact]
    public async Task RunAsync_Throws_WhenConnectionConfigMissing()
    {
        var sut = new SqlServerExplainExecutor();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RunAsync("SELECT 1", DatabaseProvider.SqlServer, null, new ExplainOptions())
        );
    }

    [Fact]
    public async Task RunAsync_UsesShowPlanXml_WhenAnalyzeDisabled()
    {
        const string xml = """
            <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan">
              <BatchSequence><Batch><Statements><StmtSimple>
                <QueryPlan>
                  <RelOp PhysicalOp="Table Scan" EstimateRows="10" EstimatedTotalSubtreeCost="1.5" />
                </QueryPlan>
              </StmtSimple></Statements></Batch></BatchSequence>
            </ShowPlanXML>
            """;

        var runner = new FakeRunner(xml);
        var sut = new SqlServerExplainExecutor(runner, new SqlServerExplainPlanParser());

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders",
            DatabaseProvider.SqlServer,
            BuildSqlServerConfig(),
            new ExplainOptions(IncludeAnalyze: false)
        );

        Assert.Equal(1, runner.ShowPlanCalls);
        Assert.Equal(0, runner.StatisticsCalls);
        Assert.Single(result.Nodes);
        Assert.Equal("SEQ SCAN", result.Nodes[0].AlertLabel);
    }

    [Fact]
    public async Task RunAsync_UsesStatisticsXml_WhenAnalyzeEnabled()
    {
        const string xml = """
            <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan">
              <BatchSequence><Batch><Statements><StmtSimple>
                <QueryPlan>
                  <RelOp PhysicalOp="Sort" EstimateRows="2" EstimatedTotalSubtreeCost="0.2" />
                </QueryPlan>
              </StmtSimple></Statements></Batch></BatchSequence>
            </ShowPlanXML>
            """;

        var runner = new FakeRunner(xml);
        var sut = new SqlServerExplainExecutor(runner, new SqlServerExplainPlanParser());

        ExplainResult result = await sut.RunAsync(
            "SELECT * FROM orders ORDER BY id",
            DatabaseProvider.SqlServer,
            BuildSqlServerConfig(),
            new ExplainOptions(IncludeAnalyze: true)
        );

        Assert.Equal(0, runner.ShowPlanCalls);
        Assert.Equal(1, runner.StatisticsCalls);
        Assert.Single(result.Nodes);
        Assert.Equal("SORT", result.Nodes[0].AlertLabel);
    }

    [Fact]
    public void BuildExplainSql_UsesExpectedScripts()
    {
        string estimated = SqlServerExplainExecutor.BuildExplainSql("SELECT 1", new ExplainOptions(IncludeAnalyze: false));
        string actual = SqlServerExplainExecutor.BuildExplainSql("SELECT 1", new ExplainOptions(IncludeAnalyze: true));

        Assert.Contains("SET SHOWPLAN_XML ON", estimated);
        Assert.Contains("SET SHOWPLAN_XML OFF", estimated);
        Assert.Contains("SET STATISTICS XML ON", actual);
        Assert.Contains("SET STATISTICS XML OFF", actual);
    }

    [Fact]
    public async Task RunAsync_DoesNotFallback_WhenStatisticsIsCancelled()
    {
        var runner = new FakeRunner("<ShowPlanXML />")
        {
            ThrowCancellationOnStatistics = true
        };
        var sut = new SqlServerExplainExecutor(runner, new SqlServerExplainPlanParser());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.RunAsync(
                "SELECT * FROM orders",
                DatabaseProvider.SqlServer,
                BuildSqlServerConfig(),
                new ExplainOptions(IncludeAnalyze: true),
                ct: new CancellationToken(canceled: true))
        );

        Assert.Equal(0, runner.ShowPlanCalls);
        Assert.Equal(1, runner.StatisticsCalls);
    }

    private static ConnectionConfig BuildSqlServerConfig() =>
        new(
            Provider: DatabaseProvider.SqlServer,
            Host: "localhost",
            Port: 1433,
            Database: "db",
            Username: "u",
            Password: "p"
        );

    private sealed class FakeRunner(string xml) : ISqlServerExplainQueryRunner
    {
        public int ShowPlanCalls { get; private set; }
        public int StatisticsCalls { get; private set; }
        public bool ThrowCancellationOnStatistics { get; init; }

        public Task<string> ExecuteShowPlanXmlAsync(
            string sql,
            ConnectionConfig connectionConfig,
            CancellationToken ct = default
        )
        {
            ShowPlanCalls++;
            return Task.FromResult(xml);
        }

        public Task<string> ExecuteStatisticsXmlAsync(
            string sql,
            ConnectionConfig connectionConfig,
            CancellationToken ct = default
        )
        {
            StatisticsCalls++;
            if (ThrowCancellationOnStatistics)
                throw new OperationCanceledException(ct);
            return Task.FromResult(xml);
        }
    }
}

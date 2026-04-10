using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainExecutorTests
{
    [Fact]
    public async Task RunAsync_UsesSqliteExecutor_WhenSqliteHasActiveConnection()
    {
        var sqlServer = new FakeExplainExecutor(false);
        var mySql = new FakeExplainExecutor(false);
        var postgres = new FakeExplainExecutor(false);
        var sqlite = new FakeExplainExecutor(false);
        var simulated = new FakeExplainExecutor(true);
        var sut = new ExplainExecutor(sqlServer, mySql, postgres, sqlite, simulated);

        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: "demo.db",
            Username: string.Empty,
            Password: string.Empty
        );

        ExplainResult result = await sut.RunAsync(
            "SELECT 1",
            DatabaseProvider.SQLite,
            cfg,
            new ExplainOptions()
        );

        Assert.False(result.IsSimulated);
        Assert.Equal(0, sqlServer.CallCount);
        Assert.Equal(0, mySql.CallCount);
        Assert.Equal(0, postgres.CallCount);
        Assert.Equal(1, sqlite.CallCount);
        Assert.Equal(0, simulated.CallCount);
    }

    [Fact]
    public async Task RunAsync_UsesPostgresExecutor_WhenPostgresHasActiveConnection()
    {
        var sqlServer = new FakeExplainExecutor(false);
        var mySql = new FakeExplainExecutor(false);
        var postgres = new FakeExplainExecutor(false);
        var sqlite = new FakeExplainExecutor(false);
        var simulated = new FakeExplainExecutor(true);
        var sut = new ExplainExecutor(sqlServer, mySql, postgres, sqlite, simulated);

        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "demo",
            Username: "u",
            Password: "p"
        );

        ExplainResult result = await sut.RunAsync(
            "SELECT 1",
            DatabaseProvider.Postgres,
            cfg,
            new ExplainOptions()
        );

        Assert.False(result.IsSimulated);
        Assert.Equal(0, sqlServer.CallCount);
        Assert.Equal(0, mySql.CallCount);
        Assert.Equal(1, postgres.CallCount);
        Assert.Equal(0, sqlite.CallCount);
        Assert.Equal(0, simulated.CallCount);
    }

    [Fact]
    public async Task RunAsync_UsesSimulatedExecutor_ForProvidersWithoutRealExecutor()
    {
        var sqlServer = new FakeExplainExecutor(false);
        var mySql = new FakeExplainExecutor(false);
        var postgres = new FakeExplainExecutor(false);
        var sqlite = new FakeExplainExecutor(false);
        var simulated = new FakeExplainExecutor(true);
        var sut = new ExplainExecutor(sqlServer, mySql, postgres, sqlite, simulated);

        ExplainResult result = await sut.RunAsync(
            "SELECT 1",
            DatabaseProvider.Postgres,
            connectionConfig: null,
            new ExplainOptions()
        );

        Assert.True(result.IsSimulated);
        Assert.Equal(0, sqlServer.CallCount);
        Assert.Equal(0, mySql.CallCount);
        Assert.Equal(0, postgres.CallCount);
        Assert.Equal(0, sqlite.CallCount);
        Assert.Equal(1, simulated.CallCount);
    }

    [Fact]
    public async Task RunAsync_UsesMySqlExecutor_WhenMySqlHasActiveConnection()
    {
        var sqlServer = new FakeExplainExecutor(false);
        var mySql = new FakeExplainExecutor(false);
        var postgres = new FakeExplainExecutor(false);
        var sqlite = new FakeExplainExecutor(false);
        var simulated = new FakeExplainExecutor(true);
        var sut = new ExplainExecutor(sqlServer, mySql, postgres, sqlite, simulated);

        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.MySql,
            Host: "localhost",
            Port: 3306,
            Database: "demo",
            Username: "u",
            Password: "p"
        );

        ExplainResult result = await sut.RunAsync(
            "SELECT 1",
            DatabaseProvider.MySql,
            cfg,
            new ExplainOptions()
        );

        Assert.False(result.IsSimulated);
        Assert.Equal(0, sqlServer.CallCount);
        Assert.Equal(1, mySql.CallCount);
        Assert.Equal(0, postgres.CallCount);
        Assert.Equal(0, sqlite.CallCount);
        Assert.Equal(0, simulated.CallCount);
    }

    [Fact]
    public async Task RunAsync_UsesSqlServerExecutor_WhenSqlServerHasActiveConnection()
    {
        var sqlServer = new FakeExplainExecutor(false);
        var mySql = new FakeExplainExecutor(false);
        var postgres = new FakeExplainExecutor(false);
        var sqlite = new FakeExplainExecutor(false);
        var simulated = new FakeExplainExecutor(true);
        var sut = new ExplainExecutor(sqlServer, mySql, postgres, sqlite, simulated);

        var cfg = new ConnectionConfig(
            Provider: DatabaseProvider.SqlServer,
            Host: "localhost",
            Port: 1433,
            Database: "demo",
            Username: "u",
            Password: "p"
        );

        ExplainResult result = await sut.RunAsync(
            "SELECT 1",
            DatabaseProvider.SqlServer,
            cfg,
            new ExplainOptions()
        );

        Assert.False(result.IsSimulated);
        Assert.Equal(1, sqlServer.CallCount);
        Assert.Equal(0, mySql.CallCount);
        Assert.Equal(0, postgres.CallCount);
        Assert.Equal(0, sqlite.CallCount);
        Assert.Equal(0, simulated.CallCount);
    }

    private sealed class FakeExplainExecutor(bool simulated) : IExplainExecutor
    {
        public int CallCount { get; private set; }

        public Task<ExplainResult> RunAsync(
            string sql,
            DatabaseProvider provider,
            ConnectionConfig? connectionConfig,
            ExplainOptions options,
            CancellationToken ct = default
        )
        {
            CallCount++;
            return Task.FromResult(
                new ExplainResult(
                    Nodes: [],
                    PlanningTimeMs: null,
                    ExecutionTimeMs: null,
                    RawOutput: "fake",
                    IsSimulated: simulated
                )
            );
        }
    }
}


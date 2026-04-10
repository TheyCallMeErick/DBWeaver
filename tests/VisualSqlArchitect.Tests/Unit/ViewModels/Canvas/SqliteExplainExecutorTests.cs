using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Microsoft.Data.Sqlite;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqliteExplainExecutorTests
{
    [Fact]
    public async Task RunAsync_ReturnsRealSqliteExplainNodes()
    {
        string dbPath = CreateTempSqlitePath();
        try
        {
            var sut = new SqliteExplainExecutor();
            ConnectionConfig config = BuildSqliteConfig(dbPath);
            await SeedAsync(config);

            ExplainResult result = await sut.RunAsync(
                "SELECT * FROM orders WHERE status = 'delivered' ORDER BY placed_at",
                DatabaseProvider.SQLite,
                config,
                new ExplainOptions()
            );

            Assert.False(result.IsSimulated);
            Assert.NotEmpty(result.Nodes);
            Assert.Contains(
                result.Nodes,
                n => n.Detail?.Contains("SEARCH", StringComparison.OrdinalIgnoreCase) == true
            );
            Assert.Contains(result.Nodes, n => n.AlertLabel == "SORT");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task RunAsync_FlagsSeqScan_WhenNoIndexUsed()
    {
        string dbPath = CreateTempSqlitePath();
        try
        {
            var sut = new SqliteExplainExecutor();
            ConnectionConfig config = BuildSqliteConfig(dbPath);
            await SeedAsync(config);

            ExplainResult result = await sut.RunAsync(
                "SELECT * FROM orders",
                DatabaseProvider.SQLite,
                config,
                new ExplainOptions()
            );

            Assert.Contains(result.Nodes, n => n.AlertLabel == "SEQ SCAN");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task RunAsync_Throws_WhenProviderIsNotSqlite()
    {
        var sut = new SqliteExplainExecutor();
        var config = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "demo",
            Username: "u",
            Password: "p"
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RunAsync("SELECT 1", DatabaseProvider.Postgres, config, new ExplainOptions())
        );
    }

    private static ConnectionConfig BuildSqliteConfig(string dbPath) =>
        new(
            Provider: DatabaseProvider.SQLite,
            Host: string.Empty,
            Port: 0,
            Database: dbPath,
            Username: string.Empty,
            Password: string.Empty
        );

    private static string CreateTempSqlitePath() =>
        Path.Combine(Path.GetTempPath(), $"vsa-explain-{Guid.NewGuid():N}.db");

    private static async Task SeedAsync(ConnectionConfig config)
    {
        var orchestrator = new DBWeaver.Providers.SqliteOrchestrator(config);
        const string sql = """
            CREATE TABLE orders (
              id INTEGER PRIMARY KEY,
              status TEXT NOT NULL,
              placed_at TEXT NOT NULL
            );
            CREATE INDEX idx_orders_status ON orders(status);
            INSERT INTO orders(status, placed_at) VALUES ('delivered','2024-01-01');
            INSERT INTO orders(status, placed_at) VALUES ('pending','2024-01-02');
            INSERT INTO orders(status, placed_at) VALUES ('delivered','2024-01-03');
            """;

        DdlExecutionResult result = await orchestrator.ExecuteDdlAsync(sql);
        Assert.True(result.Success);
    }
}



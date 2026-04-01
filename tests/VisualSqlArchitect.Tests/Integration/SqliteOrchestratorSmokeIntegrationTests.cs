using Microsoft.Data.Sqlite;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.Services;
using Xunit;

namespace VisualSqlArchitect.Tests.Integration;

public class SqliteOrchestratorSmokeIntegrationTests
{
    [Fact]
    public async Task QueryExecutorService_RealSqliteFile_ExecutesSmokeQuery()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), "vsa-smoke-" + Guid.NewGuid().ToString("N") + ".db");

        try
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath};"))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL);
INSERT INTO users (name) VALUES ('Alice'), ('Bob'), ('Carol');";
                await cmd.ExecuteNonQueryAsync();
            }

            var config = new ConnectionConfig(
                DatabaseProvider.SQLite,
                Host: "localhost",
                Port: 0,
                Database: dbPath,
                Username: string.Empty,
                Password: string.Empty,
                TimeoutSeconds: 30
            );

            var svc = new QueryExecutorService();
            var dt = await svc.ExecuteQueryAsync(config, "SELECT id, name FROM users ORDER BY id", maxRows: 2);

            Assert.NotNull(dt);
            Assert.Equal(2, dt.Rows.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // Best-effort cleanup for transient SQLite file handles in CI/test runners.
                }
            }
        }
    }
}

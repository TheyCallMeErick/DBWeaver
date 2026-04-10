using DotNet.Testcontainers.Builders;
using Npgsql;
using DBWeaver.Core;
using DBWeaver.Tests.Integration.Explain;
using DBWeaver.UI.Services;

namespace DBWeaver.Tests.Integration.ManualPlan;

public class ManualPlanInnerJoinPostgresContainerE2ETests
{
    private const string PostgresImage = "postgres:16-alpine";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InnerJoinTemplate_E2E_Postgres_GeneratesAndExecutesSql()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;
        if (!DockerImageAvailability.IsPresentLocally(PostgresImage))
            return;

        await using var container = new ContainerBuilder()
            .WithImage(PostgresImage)
            .WithEnvironment("POSTGRES_USER", "vsa_user")
            .WithEnvironment("POSTGRES_PASSWORD", "VsaPassw0rd!")
            .WithEnvironment("POSTGRES_DB", "public")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(5432),
            Database: "public",
            Username: "vsa_user",
            Password: "VsaPassw0rd!",
            TimeoutSeconds: 30
        );

        await WaitUntilPostgresReadyAsync(config, TimeSpan.FromMinutes(1));

        await using (var conn = new NpgsqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ManualPlanE2ESeedScripts.PostgresSetup;
            await cmd.ExecuteNonQueryAsync();
        }

        string sql = ManualPlanTemplateSqlGenerator.GenerateSql(DatabaseProvider.Postgres, "INNER JOIN");
        Assert.Contains("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customers", sql, StringComparison.OrdinalIgnoreCase);

        var executor = new QueryExecutorService();
        var dt = await executor.ExecuteQueryAsync(config, sql, maxRows: 1000);

        Assert.NotNull(dt);
        Assert.Equal(14, dt.Rows.Count);
        Assert.Contains(dt.Columns.Cast<System.Data.DataColumn>(), c => c.ColumnName.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WhereWithAndTemplate_E2E_Postgres_GeneratesAndExecutesSql()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;
        if (!DockerImageAvailability.IsPresentLocally(PostgresImage))
            return;

        await using var container = new ContainerBuilder()
            .WithImage(PostgresImage)
            .WithEnvironment("POSTGRES_USER", "vsa_user")
            .WithEnvironment("POSTGRES_PASSWORD", "VsaPassw0rd!")
            .WithEnvironment("POSTGRES_DB", "public")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(5432),
            Database: "public",
            Username: "vsa_user",
            Password: "VsaPassw0rd!",
            TimeoutSeconds: 30
        );

        await WaitUntilPostgresReadyAsync(config, TimeSpan.FromMinutes(1));

        await using (var conn = new NpgsqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ManualPlanE2ESeedScripts.PostgresSetup;
            await cmd.ExecuteNonQueryAsync();
        }

        string sql = ManualPlanTemplateSqlGenerator.GenerateSql(DatabaseProvider.Postgres, "WHERE com AND");
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AND", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'COMPLETED'", sql, StringComparison.OrdinalIgnoreCase);

        var executor = new QueryExecutorService();
        var dt = await executor.ExecuteQueryAsync(config, sql, maxRows: 1000);

        Assert.NotNull(dt);
        Assert.Equal(0, dt.Rows.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CountAndSumByStatusTemplate_E2E_Postgres_GeneratesAndExecutesSql()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;
        if (!DockerImageAvailability.IsPresentLocally(PostgresImage))
            return;

        await using var container = new ContainerBuilder()
            .WithImage(PostgresImage)
            .WithEnvironment("POSTGRES_USER", "vsa_user")
            .WithEnvironment("POSTGRES_PASSWORD", "VsaPassw0rd!")
            .WithEnvironment("POSTGRES_DB", "public")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(5432),
            Database: "public",
            Username: "vsa_user",
            Password: "VsaPassw0rd!",
            TimeoutSeconds: 30
        );

        await WaitUntilPostgresReadyAsync(config, TimeSpan.FromMinutes(1));

        await using (var conn = new NpgsqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ManualPlanE2ESeedScripts.PostgresSetup;
            await cmd.ExecuteNonQueryAsync();
        }

        string sql = ManualPlanTemplateSqlGenerator.GenerateSql(DatabaseProvider.Postgres, "COUNT e SUM por Status");
        Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);

        var executor = new QueryExecutorService();
        var dt = await executor.ExecuteQueryAsync(config, sql, maxRows: 1000);

        Assert.NotNull(dt);
        Assert.Equal(6, dt.Rows.Count);

        static string? GetStatus(System.Data.DataRow row) =>
            row.Table.Columns.Cast<System.Data.DataColumn>()
                .Where(c => c.ColumnName.Contains("status", StringComparison.OrdinalIgnoreCase))
                .Select(c => row[c]?.ToString())
                .FirstOrDefault();

        static int GetCount(System.Data.DataRow row) =>
            row.Table.Columns.Cast<System.Data.DataColumn>()
                .Where(c => c.ColumnName.Contains("count", StringComparison.OrdinalIgnoreCase))
                .Select(c => Convert.ToInt32(row[c]))
                .First();

        static decimal GetTotal(System.Data.DataRow row) =>
            row.Table.Columns.Cast<System.Data.DataColumn>()
                .Where(c => c.ColumnName.Contains("total", StringComparison.OrdinalIgnoreCase))
                .Select(c => Convert.ToDecimal(row[c]))
                .First();

        System.Data.DataRow delivered = dt.Rows.Cast<System.Data.DataRow>()
            .First(r => string.Equals(GetStatus(r), "delivered", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(9, GetCount(delivered));
        Assert.Equal(1470.00m, GetTotal(delivered));
    }

    private static async Task WaitUntilPostgresReadyAsync(ConnectionConfig config, TimeSpan timeout)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow - startedAt < timeout)
        {
            try
            {
                await using var conn = new NpgsqlConnection(config.BuildConnectionString());
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1;";
                _ = await cmd.ExecuteScalarAsync();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException(
            $"PostgreSQL container did not become ready within {timeout}. Last error: {lastError?.Message}",
            lastError
        );
    }
}

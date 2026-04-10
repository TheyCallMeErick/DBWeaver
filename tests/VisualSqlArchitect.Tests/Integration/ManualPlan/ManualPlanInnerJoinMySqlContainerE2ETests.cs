using MySqlConnector;
using Testcontainers.MySql;
using DBWeaver.Core;
using DBWeaver.Tests.Integration.Explain;
using DBWeaver.UI.Services;

namespace DBWeaver.Tests.Integration.ManualPlan;

public class ManualPlanInnerJoinMySqlContainerE2ETests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task InnerJoinTemplate_E2E_MySql_GeneratesAndExecutesSql()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;

        const string username = "vsa_user";
        const string password = "VsaPassw0rd!";

        await using var container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("public")
            .WithUsername(username)
            .WithPassword(password)
            .Build();
        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.MySql,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(3306),
            Database: "public",
            Username: username,
            Password: password,
            TimeoutSeconds: 30
        );

        await using (var conn = new MySqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ManualPlanE2ESeedScripts.MySqlSetup;
            await cmd.ExecuteNonQueryAsync();
        }

        string sql = ManualPlanTemplateSqlGenerator.GenerateSql(DatabaseProvider.MySql, "INNER JOIN");
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
    public async Task WhereWithAndTemplate_E2E_MySql_GeneratesAndExecutesSql()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;

        const string username = "vsa_user";
        const string password = "VsaPassw0rd!";

        await using var container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("public")
            .WithUsername(username)
            .WithPassword(password)
            .Build();
        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.MySql,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(3306),
            Database: "public",
            Username: username,
            Password: password,
            TimeoutSeconds: 30
        );

        await using (var conn = new MySqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ManualPlanE2ESeedScripts.MySqlSetup;
            await cmd.ExecuteNonQueryAsync();
        }

        string sql = ManualPlanTemplateSqlGenerator.GenerateSql(DatabaseProvider.MySql, "WHERE com AND");
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
    public async Task CountAndSumByStatusTemplate_E2E_MySql_GeneratesAndExecutesSql()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;

        const string username = "vsa_user";
        const string password = "VsaPassw0rd!";

        await using var container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("public")
            .WithUsername(username)
            .WithPassword(password)
            .Build();
        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.MySql,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(3306),
            Database: "public",
            Username: username,
            Password: password,
            TimeoutSeconds: 30
        );

        await using (var conn = new MySqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ManualPlanE2ESeedScripts.MySqlSetup;
            await cmd.ExecuteNonQueryAsync();
        }

        string sql = ManualPlanTemplateSqlGenerator.GenerateSql(DatabaseProvider.MySql, "COUNT e SUM por Status");
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

}

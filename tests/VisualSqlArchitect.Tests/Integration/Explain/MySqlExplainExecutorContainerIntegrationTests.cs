using DBWeaver.UI.Services.Explain;
using MySqlConnector;
using Testcontainers.MySql;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Integration.Explain;

public class MySqlExplainExecutorContainerIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MySqlExplainExecutor_Container_ReturnsRealPlan_ForEstimateAndAnalyze()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;

        const string username = "vsa_user";
        const string password = "VsaPassw0rd!";
        await using var container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("vsa")
            .WithUsername(username)
            .WithPassword(password)
            .Build();
        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.MySql,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(3306),
            Database: "vsa",
            Username: username,
            Password: password,
            TimeoutSeconds: 30
        );

        await using (var conn = new MySqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var seed = conn.CreateCommand();
            seed.CommandText = @"
CREATE TABLE IF NOT EXISTS explain_users (
    id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(64) NOT NULL
);
INSERT INTO explain_users (name)
SELECT 'alice'
WHERE NOT EXISTS (SELECT 1 FROM explain_users);
";
            await seed.ExecuteNonQueryAsync();
        }

        const string sqlUnderTest = "SELECT id FROM explain_users WHERE id > 0 ORDER BY id;";
        var executor = new MySqlExplainExecutor();
        ExplainResult estimate = await executor.RunAsync(
            sql: sqlUnderTest,
            provider: DatabaseProvider.MySql,
            connectionConfig: config,
            options: new ExplainOptions(IncludeAnalyze: false, IncludeBuffers: false, Format: ExplainFormat.Json),
            ct: CancellationToken.None
        );

        ExplainResult analyze = await executor.RunAsync(
            sql: sqlUnderTest,
            provider: DatabaseProvider.MySql,
            connectionConfig: config,
            options: new ExplainOptions(IncludeAnalyze: true, IncludeBuffers: false, Format: ExplainFormat.Json),
            ct: CancellationToken.None
        );

        Assert.False(estimate.IsSimulated);
        Assert.False(string.IsNullOrWhiteSpace(estimate.RawOutput));
        Assert.NotEmpty(estimate.Nodes);
        Assert.False(analyze.IsSimulated);
        Assert.False(string.IsNullOrWhiteSpace(analyze.RawOutput));
        Assert.NotEmpty(analyze.Nodes);
    }
}


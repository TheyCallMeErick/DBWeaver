using DotNet.Testcontainers.Builders;
using Npgsql;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Integration.Explain;

public class PostgresExplainExecutorContainerIntegrationTests
{
    private const string PostgresImage = "postgres:16-alpine";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresExplainExecutor_Container_ReturnsRealPlan_ForEstimateAndAnalyze()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;
        if (!DockerImageAvailability.IsPresentLocally(PostgresImage))
            return;

        await using var container = new ContainerBuilder()
            .WithImage(PostgresImage)
            .WithEnvironment("POSTGRES_USER", "vsa_user")
            .WithEnvironment("POSTGRES_PASSWORD", "VsaPassw0rd!")
            .WithEnvironment("POSTGRES_DB", "vsa")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();
        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.Postgres,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(5432),
            Database: "vsa",
            Username: "vsa_user",
            Password: "VsaPassw0rd!",
            TimeoutSeconds: 30
        );
        await WaitUntilPostgresReadyAsync(config, TimeSpan.FromMinutes(1));

        await using (var conn = new NpgsqlConnection(config.BuildConnectionString()))
        {
            await conn.OpenAsync();
            await using var seed = conn.CreateCommand();
            seed.CommandText = @"
CREATE TABLE IF NOT EXISTS explain_users (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL
);
INSERT INTO explain_users(name)
SELECT 'alice'
WHERE NOT EXISTS (SELECT 1 FROM explain_users);";
            await seed.ExecuteNonQueryAsync();
        }

        const string sqlUnderTest = "SELECT id FROM explain_users WHERE id > 0 ORDER BY id;";
        var executor = new PostgresExplainExecutor();
        ExplainResult estimate = await executor.RunAsync(
            sql: sqlUnderTest,
            provider: DatabaseProvider.Postgres,
            connectionConfig: config,
            options: new ExplainOptions(IncludeAnalyze: false, IncludeBuffers: false, Format: ExplainFormat.Json),
            ct: CancellationToken.None
        );
        ExplainResult analyze = await executor.RunAsync(
            sql: sqlUnderTest,
            provider: DatabaseProvider.Postgres,
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


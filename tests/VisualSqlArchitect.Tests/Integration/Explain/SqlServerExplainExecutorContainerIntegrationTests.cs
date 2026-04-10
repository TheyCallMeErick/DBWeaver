using DBWeaver.UI.Services.Explain;
using DotNet.Testcontainers.Builders;
using Testcontainers.MsSql;
using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Integration.Explain;

public class SqlServerExplainExecutorContainerIntegrationTests
{
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqlServerExplainExecutor_Container_ReturnsRealPlan_ForEstimateAndAnalyze()
    {
        if (!DockerIntegrationRequirements.IsEnabled())
            return;
        if (!DockerImageAvailability.IsPresentLocally(SqlServerImage))
            return;

        const string saPassword = "YourStrong!Passw0rd";
        await using var container = new MsSqlBuilder()
            .WithImage(SqlServerImage)
            .WithPassword(saPassword)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilPortIsAvailable(1433)
            )
            .Build();
        await container.StartAsync();

        var config = new ConnectionConfig(
            Provider: DatabaseProvider.SqlServer,
            Host: "127.0.0.1",
            Port: container.GetMappedPublicPort(1433),
            Database: "master",
            Username: "sa",
            Password: saPassword,
            TimeoutSeconds: 30
        );
        await WaitUntilSqlServerLoginReadyAsync(config, TimeSpan.FromMinutes(2));

        const string sqlUnderTest = "SELECT TOP 1 name FROM sys.objects ORDER BY name;";
        var runner = new SqlServerExplainQueryRunner();
        string rawShowPlan = await runner.ExecuteShowPlanXmlAsync(sqlUnderTest, config, CancellationToken.None);
        if (!rawShowPlan.Contains("<ShowPlanXML", StringComparison.OrdinalIgnoreCase))
        {
            string preview = rawShowPlan.Length > 240 ? rawShowPlan[..240] : rawShowPlan;
            throw new Xunit.Sdk.XunitException($"Unexpected SHOWPLAN payload: [{preview}]");
        }

        var executor = new SqlServerExplainExecutor();
        ExplainResult estimate = await executor.RunAsync(
            sql: sqlUnderTest,
            provider: DatabaseProvider.SqlServer,
            connectionConfig: config,
            options: new ExplainOptions(IncludeAnalyze: false, IncludeBuffers: false, Format: ExplainFormat.Xml),
            ct: CancellationToken.None
        );

        ExplainResult analyze = await executor.RunAsync(
            sql: sqlUnderTest,
            provider: DatabaseProvider.SqlServer,
            connectionConfig: config,
            options: new ExplainOptions(IncludeAnalyze: true, IncludeBuffers: false, Format: ExplainFormat.Xml),
            ct: CancellationToken.None
        );

        Assert.False(estimate.IsSimulated);
        Assert.Contains("ShowPlanXML", estimate.RawOutput, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(estimate.Nodes);
        Assert.False(analyze.IsSimulated);
        Assert.Contains("ShowPlanXML", analyze.RawOutput, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(analyze.Nodes);
    }

    private static async Task WaitUntilSqlServerLoginReadyAsync(ConnectionConfig config, TimeSpan timeout)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow - startedAt < timeout)
        {
            try
            {
                await using var conn = new Microsoft.Data.SqlClient.SqlConnection(config.BuildConnectionString());
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
            $"SQL Server container did not become login-ready within {timeout}. Last error: {lastError?.Message}",
            lastError
        );
    }
}


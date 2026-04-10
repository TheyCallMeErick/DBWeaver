using DBWeaver.Core;
using DBWeaver.Providers;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class DbOrchestratorConnectionTestExecutor : IConnectionTestExecutor
{
    public async Task<ConnectionTestResult> ExecuteAsync(
        ConnectionConfig config,
        DatabaseProvider provider,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        IDbOrchestrator orchestrator = provider switch
        {
            DatabaseProvider.Postgres => new PostgresOrchestrator(config),
            DatabaseProvider.MySql => new MySqlOrchestrator(config),
            DatabaseProvider.SqlServer => new SqlServerOrchestrator(config),
            DatabaseProvider.SQLite => new SqliteOrchestrator(config),
            _ => throw new NotSupportedException($"Unknown provider: {provider}"),
        };

        await using (orchestrator)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return await orchestrator.TestConnectionAsync(cts.Token);
        }
    }
}


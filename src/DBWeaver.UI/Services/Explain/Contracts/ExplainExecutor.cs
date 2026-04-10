using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public sealed class ExplainExecutor : IExplainExecutor
{
    private readonly IExplainExecutor _sqlServerExecutor;
    private readonly IExplainExecutor _mySqlExecutor;
    private readonly IExplainExecutor _postgresExecutor;
    private readonly IExplainExecutor _sqliteExecutor;
    private readonly IExplainExecutor _simulatedExecutor;

    public ExplainExecutor(
        IExplainExecutor? sqlServerExecutor = null,
        IExplainExecutor? mySqlExecutor = null,
        IExplainExecutor? postgresExecutor = null,
        IExplainExecutor? sqliteExecutor = null,
        IExplainExecutor? simulatedExecutor = null
    )
    {
        _sqlServerExecutor = sqlServerExecutor ?? new SqlServerExplainExecutor();
        _mySqlExecutor = mySqlExecutor ?? new MySqlExplainExecutor();
        _postgresExecutor = postgresExecutor ?? new PostgresExplainExecutor();
        _sqliteExecutor = sqliteExecutor ?? new SqliteExplainExecutor();
        _simulatedExecutor = simulatedExecutor ?? new SimulatedExplainExecutor();
    }

    public Task<ExplainResult> RunAsync(
        string sql,
        DatabaseProvider provider,
        ConnectionConfig? connectionConfig,
        ExplainOptions options,
        CancellationToken ct = default
    )
    {
        if (
            provider == DatabaseProvider.SqlServer &&
            connectionConfig is { Provider: DatabaseProvider.SqlServer }
        )
        {
            return _sqlServerExecutor.RunAsync(sql, provider, connectionConfig, options, ct);
        }

        if (
            provider == DatabaseProvider.MySql &&
            connectionConfig is { Provider: DatabaseProvider.MySql }
        )
        {
            return _mySqlExecutor.RunAsync(sql, provider, connectionConfig, options, ct);
        }

        if (
            provider == DatabaseProvider.Postgres &&
            connectionConfig is { Provider: DatabaseProvider.Postgres }
        )
        {
            return _postgresExecutor.RunAsync(sql, provider, connectionConfig, options, ct);
        }

        if (
            provider == DatabaseProvider.SQLite &&
            connectionConfig is { Provider: DatabaseProvider.SQLite }
        )
        {
            return _sqliteExecutor.RunAsync(sql, provider, connectionConfig, options, ct);
        }

        return _simulatedExecutor.RunAsync(sql, provider, connectionConfig, options, ct);
    }
}




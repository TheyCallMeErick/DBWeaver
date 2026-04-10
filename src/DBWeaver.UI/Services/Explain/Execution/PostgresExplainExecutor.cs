using Npgsql;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.Core;

namespace DBWeaver.UI.Services.Explain;

public interface IPostgresExplainQueryRunner
{
    Task<string> ExecuteAsync(
        string explainSql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );
}

public sealed class PostgresExplainQueryRunner : IPostgresExplainQueryRunner
{
    public async Task<string> ExecuteAsync(
        string explainSql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    )
    {
        await using var conn = new NpgsqlConnection(connectionConfig.BuildConnectionString());
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = connectionConfig.TimeoutSeconds;
        cmd.CommandText = explainSql;

        object? payload = await cmd.ExecuteScalarAsync(ct);
        return payload?.ToString() ?? "[]";
    }
}

public sealed class PostgresExplainExecutor : IExplainExecutor
{
    private readonly IPostgresExplainPlanParser _parser;
    private readonly IPostgresExplainQueryRunner _queryRunner;

    public PostgresExplainExecutor(
        IPostgresExplainPlanParser? parser = null,
        IPostgresExplainQueryRunner? queryRunner = null
    )
    {
        _parser = parser ?? new PostgresExplainPlanParser();
        _queryRunner = queryRunner ?? new PostgresExplainQueryRunner();
    }

    public async Task<ExplainResult> RunAsync(
        string sql,
        DatabaseProvider provider,
        ConnectionConfig? connectionConfig,
        ExplainOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        if (provider != DatabaseProvider.Postgres)
            throw new ArgumentException("Provider must be Postgres.", nameof(provider));
        if (connectionConfig is null || connectionConfig.Provider != DatabaseProvider.Postgres)
            throw new ArgumentException("A valid Postgres connection config is required.", nameof(connectionConfig));

        string explainSql = BuildExplainSql(sql, options);
        string raw = await _queryRunner.ExecuteAsync(explainSql, connectionConfig, ct);

        PostgresParsedPlan parsed = _parser.Parse(raw);
        return new ExplainResult(
            Nodes: parsed.Nodes,
            PlanningTimeMs: parsed.PlanningTimeMs,
            ExecutionTimeMs: parsed.ExecutionTimeMs,
            RawOutput: raw,
            IsSimulated: false
        );
    }

    public static string BuildExplainSql(string sql, ExplainOptions options)
    {
        string format = options.Format switch
        {
            ExplainFormat.Text => "TEXT",
            ExplainFormat.Xml => "XML",
            _ => "JSON",
        };

        var clauses = new List<string> { $"FORMAT {format}" };
        if (options.IncludeAnalyze)
            clauses.Add("ANALYZE TRUE");
        if (options.IncludeAnalyze && options.IncludeBuffers)
            clauses.Add("BUFFERS TRUE");

        return $"EXPLAIN ({string.Join(", ", clauses)}) {sql}";
    }
}




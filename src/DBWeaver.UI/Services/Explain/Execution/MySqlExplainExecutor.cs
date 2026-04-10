using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public sealed class MySqlExplainExecutor : IExplainExecutor
{
    private readonly IMySqlExplainQueryRunner _queryRunner;
    private readonly IMySqlExplainPlanParser _parser;

    public MySqlExplainExecutor(
        IMySqlExplainQueryRunner? queryRunner = null,
        IMySqlExplainPlanParser? parser = null
    )
    {
        _queryRunner = queryRunner ?? new MySqlExplainQueryRunner();
        _parser = parser ?? new MySqlExplainPlanParser();
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

        if (provider != DatabaseProvider.MySql)
            throw new ArgumentException("Provider must be MySql.", nameof(provider));
        if (connectionConfig is null || connectionConfig.Provider != DatabaseProvider.MySql)
            throw new ArgumentException("A valid MySql connection config is required.", nameof(connectionConfig));

        if (options.IncludeAnalyze)
            return await RunAnalyzeWithFallbackAsync(sql, connectionConfig, ct);

        string rawJson = await _queryRunner.ExecuteFormatJsonAsync(sql, connectionConfig, ct);
        return ToResult(_parser.ParseJson(rawJson), rawJson);
    }

    private async Task<ExplainResult> RunAnalyzeWithFallbackAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct)
    {
        Exception? analyzeFailure = null;
        try
        {
            string rawAnalyze = await _queryRunner.ExecuteAnalyzeAsync(sql, connectionConfig, ct);
            return ToResult(_parser.ParseAnalyze(rawAnalyze), rawAnalyze);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            analyzeFailure = ex;
        }

        try
        {
            string rawJson = await _queryRunner.ExecuteFormatJsonAsync(sql, connectionConfig, ct);
            return ToResult(_parser.ParseJson(rawJson), rawJson);
        }
        catch (Exception jsonFailure)
        {
            throw new InvalidOperationException(
                "MySQL EXPLAIN failed for both ANALYZE and FORMAT=JSON. Validate server version/permissions and SQL syntax.",
                new AggregateException(analyzeFailure!, jsonFailure));
        }
    }

    private static ExplainResult ToResult(MySqlParsedPlan parsed, string rawOutput) =>
        new(
            Nodes: parsed.Nodes,
            PlanningTimeMs: parsed.PlanningTimeMs,
            ExecutionTimeMs: parsed.ExecutionTimeMs,
            RawOutput: rawOutput,
            IsSimulated: false
        );
}



using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public sealed class SqlServerExplainExecutor : IExplainExecutor
{
    private readonly ISqlServerExplainQueryRunner _queryRunner;
    private readonly ISqlServerExplainPlanParser _parser;

    public SqlServerExplainExecutor(
        ISqlServerExplainQueryRunner? queryRunner = null,
        ISqlServerExplainPlanParser? parser = null
    )
    {
        _queryRunner = queryRunner ?? new SqlServerExplainQueryRunner();
        _parser = parser ?? new SqlServerExplainPlanParser();
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

        if (provider != DatabaseProvider.SqlServer)
            throw new ArgumentException("Provider must be SqlServer.", nameof(provider));
        if (connectionConfig is null || connectionConfig.Provider != DatabaseProvider.SqlServer)
            throw new ArgumentException("A valid SqlServer connection config is required.", nameof(connectionConfig));

        if (options.IncludeAnalyze)
        {
            try
            {
                string rawStatisticsXml = await _queryRunner.ExecuteStatisticsXmlAsync(sql, connectionConfig, ct);
                return Parse(rawStatisticsXml);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fallback keeps SQL Server usable when STATISTICS XML output shape varies by server/driver.
                string rawShowPlanXml = await _queryRunner.ExecuteShowPlanXmlAsync(sql, connectionConfig, ct);
                return Parse(rawShowPlanXml);
            }
        }

        string rawXml = await _queryRunner.ExecuteShowPlanXmlAsync(sql, connectionConfig, ct);
        return Parse(rawXml);
    }

    private ExplainResult Parse(string rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
        {
            throw new InvalidOperationException(
                "SQL Server EXPLAIN returned empty plan XML. Ensure SHOWPLAN/STATISTICS XML permissions are granted."
            );
        }

        SqlServerParsedPlan parsed = _parser.Parse(rawXml);
        return new ExplainResult(
            Nodes: parsed.Nodes,
            PlanningTimeMs: parsed.PlanningTimeMs,
            ExecutionTimeMs: parsed.ExecutionTimeMs,
            RawOutput: rawXml,
            IsSimulated: false
        );
    }

    public static string BuildExplainSql(string sql, ExplainOptions options)
    {
        return options.IncludeAnalyze
            ? $"SET STATISTICS XML ON; {sql}; SET STATISTICS XML OFF;"
            : $"SET SHOWPLAN_XML ON; {sql}; SET SHOWPLAN_XML OFF;";
    }
}



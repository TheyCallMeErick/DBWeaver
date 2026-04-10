using DBWeaver.Core;
using DBWeaver.UI.Services;

namespace DBWeaver.UI.Services.Benchmark;

/// <summary>
/// Executes real benchmark iterations when a connection + SQL are available.
/// Falls back to simulated latency sampling when runtime context is incomplete.
/// </summary>
public sealed class AdaptiveBenchmarkIterationExecutor : IBenchmarkIterationExecutor
{
    private readonly Func<ConnectionConfig?> _connectionResolver;
    private readonly Func<string> _sqlResolver;
    private readonly QueryExecutorService _queryExecutorService;
    private readonly IBenchmarkIterationExecutor _fallbackExecutor;
    private readonly int _maxRows;

    public AdaptiveBenchmarkIterationExecutor(
        Func<ConnectionConfig?> connectionResolver,
        Func<string> sqlResolver,
        QueryExecutorService? queryExecutorService = null,
        IBenchmarkIterationExecutor? fallbackExecutor = null,
        int maxRows = 100)
    {
        _connectionResolver = connectionResolver;
        _sqlResolver = sqlResolver;
        _queryExecutorService = queryExecutorService ?? new QueryExecutorService();
        _fallbackExecutor = fallbackExecutor ?? new SimulatedBenchmarkIterationExecutor();
        _maxRows = Math.Clamp(maxRows, 1, 10_000);
    }

    public async Task<double> ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        ConnectionConfig? connection = _connectionResolver();
        string sql = _sqlResolver();

        if (connection is null || string.IsNullOrWhiteSpace(sql))
            return await _fallbackExecutor.ExecuteIterationAsync(cancellationToken);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _queryExecutorService.ExecuteQueryAsync(
            connection,
            sql,
            maxRows: _maxRows,
            ct: cancellationToken);
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
}


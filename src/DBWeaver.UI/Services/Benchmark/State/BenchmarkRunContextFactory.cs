namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkRunContextFactory(IBenchmarkTextProvider textProvider) : IBenchmarkRunContextFactory
{
    private readonly IBenchmarkTextProvider _textProvider = textProvider;

    public BenchmarkRunContextCreationResult TryCreate(
        string rawSql,
        int iterations,
        int warmupIterations,
        int intervalMs)
    {
        if (string.IsNullOrWhiteSpace(rawSql))
            return new BenchmarkRunContextCreationResult(
                Context: null,
                RejectionMessage: _textProvider.NoSqlToBenchmark());

        var configuration = new BenchmarkRunConfiguration(iterations, warmupIterations, intervalMs).Normalize();
        var context = new BenchmarkRunContext(rawSql, configuration, new CancellationTokenSource());
        return new BenchmarkRunContextCreationResult(context, RejectionMessage: null);
    }
}


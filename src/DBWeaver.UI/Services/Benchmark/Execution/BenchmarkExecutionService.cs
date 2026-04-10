namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkExecutionService(IBenchmarkRunner benchmarkRunner) : IBenchmarkExecutionService
{
    private readonly IBenchmarkRunner _benchmarkRunner = benchmarkRunner;

    public async Task<BenchmarkRunResult> ExecuteAsync(
        string runLabel,
        BenchmarkRunConfiguration configuration,
        Action<BenchmarkRunProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        BenchmarkRunConfiguration normalizedConfiguration = configuration.Normalize();
        IReadOnlyList<double> latencies = await _benchmarkRunner.RunAsync(
            normalizedConfiguration,
            onProgress,
            cancellationToken);

        return BenchmarkStatisticsCalculator.BuildResult(
            runLabel,
            normalizedConfiguration.Iterations,
            latencies,
            DateTime.Now);
    }
}


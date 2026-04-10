namespace DBWeaver.UI.Services.Benchmark;

public interface IBenchmarkExecutionService
{
    Task<BenchmarkRunResult> ExecuteAsync(
        string runLabel,
        BenchmarkRunConfiguration configuration,
        Action<BenchmarkRunProgress>? onProgress,
        CancellationToken cancellationToken);
}


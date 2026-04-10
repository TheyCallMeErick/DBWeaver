namespace DBWeaver.UI.Services.Benchmark;

public interface IBenchmarkRunner
{
    Task<IReadOnlyList<double>> RunAsync(
        BenchmarkRunConfiguration configuration,
        Action<BenchmarkRunProgress>? onProgress,
        CancellationToken cancellationToken);
}


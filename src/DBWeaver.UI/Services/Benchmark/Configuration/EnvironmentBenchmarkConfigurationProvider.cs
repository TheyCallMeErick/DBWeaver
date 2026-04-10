namespace DBWeaver.UI.Services.Benchmark;

public sealed class EnvironmentBenchmarkConfigurationProvider : IBenchmarkConfigurationProvider
{
    public const string IterationsEnvVar = "VSA_BENCHMARK_ITERATIONS";
    public const string WarmupEnvVar = "VSA_BENCHMARK_WARMUP_ITERATIONS";
    public const string IntervalEnvVar = "VSA_BENCHMARK_INTERVAL_MS";

    public BenchmarkRunConfiguration GetDefaultConfiguration()
    {
        int iterations = ReadInt(IterationsEnvVar, fallback: 10);
        int warmup = ReadInt(WarmupEnvVar, fallback: 2);
        int intervalMs = ReadInt(IntervalEnvVar, fallback: 0);

        return new BenchmarkRunConfiguration(iterations, warmup, intervalMs).Normalize();
    }

    private static int ReadInt(string envVar, int fallback)
    {
        string? raw = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(raw, out int value) ? value : fallback;
    }
}


namespace DBWeaver.UI.Services.Benchmark;

public interface IBenchmarkRunContextFactory
{
    BenchmarkRunContextCreationResult TryCreate(
        string rawSql,
        int iterations,
        int warmupIterations,
        int intervalMs);
}


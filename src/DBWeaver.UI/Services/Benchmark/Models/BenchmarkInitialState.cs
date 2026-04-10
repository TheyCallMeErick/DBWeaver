namespace DBWeaver.UI.Services.Benchmark;

public readonly record struct BenchmarkInitialState(
    int Iterations,
    int WarmupIterations,
    int IntervalMs,
    string RunLabel);


namespace DBWeaver.UI.Services.Benchmark;

public readonly record struct BenchmarkRunConfiguration(int Iterations, int WarmupIterations, int IntervalMs)
{
    public BenchmarkRunConfiguration Normalize() =>
        new(
            NormalizeIterations(Iterations),
            NormalizeWarmupIterations(WarmupIterations),
            NormalizeIntervalMs(IntervalMs)
        );

    public static int NormalizeIterations(int value) => Math.Clamp(value, 1, 100);

    public static int NormalizeWarmupIterations(int value) => Math.Clamp(value, 0, 10);

    public static int NormalizeIntervalMs(int value) => Math.Clamp(value, 0, 5000);
}


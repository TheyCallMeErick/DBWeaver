namespace DBWeaver.UI.Services.Benchmark;

public readonly record struct BenchmarkRunProgress(
    BenchmarkRunStage Stage,
    int Completed,
    int Total,
    double? MeasuredLatencyMs);


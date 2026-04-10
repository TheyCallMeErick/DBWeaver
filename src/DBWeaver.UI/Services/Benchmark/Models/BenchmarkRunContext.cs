namespace DBWeaver.UI.Services.Benchmark;

public readonly record struct BenchmarkRunContext(
    string Sql,
    BenchmarkRunConfiguration Configuration,
    CancellationTokenSource CancellationTokenSource);


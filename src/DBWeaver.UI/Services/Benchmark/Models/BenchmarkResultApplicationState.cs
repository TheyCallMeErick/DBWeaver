namespace DBWeaver.UI.Services.Benchmark;

public readonly record struct BenchmarkResultApplicationState(
    BenchmarkRunResult LatestResult,
    string Progress,
    string NextRunLabel);


namespace DBWeaver.UI.Services.Benchmark;

public readonly record struct BenchmarkRunUiState(
    bool IsRunning,
    string Progress,
    double ProgressFraction);


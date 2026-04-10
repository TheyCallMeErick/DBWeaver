namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkRunStateCoordinator(IBenchmarkTextProvider textProvider) : IBenchmarkRunStateCoordinator
{
    private readonly IBenchmarkTextProvider _textProvider = textProvider;

    public BenchmarkRunUiState BuildStartState() =>
        new(IsRunning: true, Progress: string.Empty, ProgressFraction: 0);

    public string BuildCancelledMessage() => _textProvider.Cancelled();

    public string BuildFailureMessage(string reason) => _textProvider.FailedWithReason(reason);

    public BenchmarkRunUiState BuildFinishState(string currentProgress) =>
        new(IsRunning: false, Progress: currentProgress, ProgressFraction: 0);
}


namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkProgressPresenter(IBenchmarkTextProvider textProvider) : IBenchmarkProgressPresenter
{
    private readonly IBenchmarkTextProvider _textProvider = textProvider;

    public BenchmarkProgressViewState Build(
        BenchmarkRunProgress progress,
        int warmupIterations,
        int iterations)
    {
        string message = progress.Stage == BenchmarkRunStage.Warmup
            ? _textProvider.WarmupProgress(progress.Completed, warmupIterations)
            : _textProvider.IterationProgress(progress.Completed - warmupIterations, iterations);

        double fraction = progress.Total <= 0
            ? 0
            : (double)progress.Completed / progress.Total;

        return new BenchmarkProgressViewState(message, fraction);
    }
}


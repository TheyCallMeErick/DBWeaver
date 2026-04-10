using System.Collections.ObjectModel;

namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkResultCoordinator(IBenchmarkTextProvider textProvider) : IBenchmarkResultCoordinator
{
    private readonly IBenchmarkTextProvider _textProvider = textProvider;

    public BenchmarkResultApplicationState ApplySuccess(
        ObservableCollection<BenchmarkRunResult> history,
        BenchmarkRunResult result)
    {
        history.Insert(0, result);

        string progress = _textProvider.Completed(result.Summary);
        string nextRunLabel = _textProvider.BuildRunLabel(history.Count + 1);

        return new BenchmarkResultApplicationState(result, progress, nextRunLabel);
    }
}


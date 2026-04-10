using System.Collections.ObjectModel;

namespace DBWeaver.UI.Services.Benchmark;

public interface IBenchmarkResultCoordinator
{
    BenchmarkResultApplicationState ApplySuccess(
        ObservableCollection<BenchmarkRunResult> history,
        BenchmarkRunResult result);
}


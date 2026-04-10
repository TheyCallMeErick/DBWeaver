using DBWeaver.UI.Services.Benchmark;
using System.Collections.ObjectModel;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkResultCoordinatorTests
{
    [Fact]
    public void ApplySuccess_InsertsResultAtTopAndBuildsUiState()
    {
        var text = new BenchmarkTestDoubles.FakeBenchmarkTextProvider();
        var coordinator = new BenchmarkResultCoordinator(text);
        var history = new ObservableCollection<BenchmarkRunResult>
        {
            new("Older", 1, 1, 1, 1, 1, 1, DateTime.Now)
        };
        var result = new BenchmarkRunResult("New", 3, 1, 5, 3, 3, 5, DateTime.Now);

        BenchmarkResultApplicationState state = coordinator.ApplySuccess(history, result);

        Assert.Equal(2, history.Count);
        Assert.Same(result, history[0]);
        Assert.Same(result, state.LatestResult);
        Assert.StartsWith("DONE:", state.Progress, StringComparison.Ordinal);
        Assert.Equal("RUN#3", state.NextRunLabel);
    }
}


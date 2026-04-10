using DBWeaver.UI.Services.Benchmark;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkRunStateCoordinatorTests
{
    [Fact]
    public void BuildStartState_InitializesRunningState()
    {
        var coordinator = new BenchmarkRunStateCoordinator(new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        BenchmarkRunUiState state = coordinator.BuildStartState();

        Assert.True(state.IsRunning);
        Assert.Equal(string.Empty, state.Progress);
        Assert.Equal(0, state.ProgressFraction);
    }

    [Fact]
    public void BuildCancelledMessage_UsesTextProvider()
    {
        var coordinator = new BenchmarkRunStateCoordinator(new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        Assert.Equal("CANCELLED", coordinator.BuildCancelledMessage());
    }

    [Fact]
    public void BuildFailureMessage_UsesTextProvider()
    {
        var coordinator = new BenchmarkRunStateCoordinator(new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        Assert.Equal("FAILED:oops", coordinator.BuildFailureMessage("oops"));
    }

    [Fact]
    public void BuildFinishState_StopsRunAndResetsFraction()
    {
        var coordinator = new BenchmarkRunStateCoordinator(new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        BenchmarkRunUiState state = coordinator.BuildFinishState("DONE");

        Assert.False(state.IsRunning);
        Assert.Equal("DONE", state.Progress);
        Assert.Equal(0, state.ProgressFraction);
    }
}


using DBWeaver.UI.Services.Benchmark;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkProgressPresenterTests
{
    [Fact]
    public void Build_WarmupStage_UsesWarmupMessageAndFraction()
    {
        var text = new BenchmarkTestDoubles.FakeBenchmarkTextProvider();
        var presenter = new BenchmarkProgressPresenter(text);

        BenchmarkProgressViewState viewState = presenter.Build(
            new BenchmarkRunProgress(BenchmarkRunStage.Warmup, Completed: 1, Total: 5, MeasuredLatencyMs: null),
            warmupIterations: 2,
            iterations: 3);

        Assert.Equal("WARMUP:1/2", viewState.Message);
        Assert.Equal(0.2, viewState.Fraction, 3);
    }

    [Fact]
    public void Build_IterationStage_UsesIterationMessageAndFraction()
    {
        var text = new BenchmarkTestDoubles.FakeBenchmarkTextProvider();
        var presenter = new BenchmarkProgressPresenter(text);

        BenchmarkProgressViewState viewState = presenter.Build(
            new BenchmarkRunProgress(BenchmarkRunStage.Iteration, Completed: 4, Total: 5, MeasuredLatencyMs: 15),
            warmupIterations: 2,
            iterations: 3);

        Assert.Equal("ITER:2/3", viewState.Message);
        Assert.Equal(0.8, viewState.Fraction, 3);
    }

    [Fact]
    public void Build_TotalZero_UsesZeroFraction()
    {
        var text = new BenchmarkTestDoubles.FakeBenchmarkTextProvider();
        var presenter = new BenchmarkProgressPresenter(text);

        BenchmarkProgressViewState viewState = presenter.Build(
            new BenchmarkRunProgress(BenchmarkRunStage.Warmup, Completed: 0, Total: 0, MeasuredLatencyMs: null),
            warmupIterations: 1,
            iterations: 1);

        Assert.Equal(0, viewState.Fraction);
    }
}


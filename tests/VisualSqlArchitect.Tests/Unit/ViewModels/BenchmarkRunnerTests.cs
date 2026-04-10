using DBWeaver.UI.Services.Benchmark;


namespace DBWeaver.Tests.Unit.ViewModels;

public sealed class BenchmarkRunnerTests
{
    [Fact]
    public async Task RunAsync_CollectsOnlyMeasuredLatencies_AndReportsProgress()
    {
        var executor = new BenchmarkTestDoubles.SequenceIterationExecutor([5, 7, 11, 13]);
        var runner = new BenchmarkRunner(executor);
        var progress = new List<BenchmarkRunProgress>();

        IReadOnlyList<double> latencies = await runner.RunAsync(
            new BenchmarkRunConfiguration(Iterations: 2, WarmupIterations: 2, IntervalMs: 0),
            progress.Add,
            CancellationToken.None);

        Assert.Equal([11d, 13d], latencies);
        Assert.Equal(4, progress.Count);
        Assert.Equal(BenchmarkRunStage.Warmup, progress[0].Stage);
        Assert.Equal(BenchmarkRunStage.Iteration, progress[^1].Stage);
        Assert.Equal(4, progress[^1].Completed);
        Assert.Equal(4, progress[^1].Total);
    }

    [Fact]
    public async Task RunAsync_ThrowsWhenCancelled()
    {
        var executor = new BenchmarkTestDoubles.SequenceIterationExecutor([10, 20, 30]);
        var runner = new BenchmarkRunner(executor);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync(new BenchmarkRunConfiguration(3, 0, 0), onProgress: null, cts.Token));
    }

    [Fact]
    public async Task RunAsync_UsesDelayScheduler_WhenIntervalPositive()
    {
        var executor = new BenchmarkTestDoubles.SequenceIterationExecutor([1, 2, 3]);
        var delay = new BenchmarkTestDoubles.RecordingDelayScheduler();
        var runner = new BenchmarkRunner(executor, delay);

        await runner.RunAsync(
            new BenchmarkRunConfiguration(Iterations: 2, WarmupIterations: 1, IntervalMs: 15),
            onProgress: null,
            CancellationToken.None);

        Assert.Equal(3, delay.CallCount);
        Assert.All(delay.Milliseconds, ms => Assert.Equal(15, ms));
    }

    [Fact]
    public async Task RunAsync_DoesNotUseDelayScheduler_WhenIntervalZero()
    {
        var executor = new BenchmarkTestDoubles.SequenceIterationExecutor([1, 2, 3]);
        var delay = new BenchmarkTestDoubles.RecordingDelayScheduler();
        var runner = new BenchmarkRunner(executor, delay);

        await runner.RunAsync(
            new BenchmarkRunConfiguration(Iterations: 2, WarmupIterations: 1, IntervalMs: 0),
            onProgress: null,
            CancellationToken.None);

        Assert.Equal(0, delay.CallCount);
    }
}


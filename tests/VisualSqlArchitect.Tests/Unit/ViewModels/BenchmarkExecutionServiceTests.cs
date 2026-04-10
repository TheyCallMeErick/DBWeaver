using DBWeaver.UI.Services.Benchmark;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_BuildsResultFromRunnerLatencies()
    {
        var executor = new BenchmarkTestDoubles.SequenceIterationExecutor([10, 20, 30]);
        var runner = new BenchmarkRunner(executor);
        var service = new BenchmarkExecutionService(runner);

        BenchmarkRunResult result = await service.ExecuteAsync(
            runLabel: "R1",
            configuration: new BenchmarkRunConfiguration(Iterations: 3, WarmupIterations: 0, IntervalMs: 0),
            onProgress: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal("R1", result.Label);
        Assert.Equal(3, result.Iterations);
        Assert.Equal(10, result.MinMs);
        Assert.Equal(30, result.MaxMs);
        Assert.Equal(20, result.AvgMs, 3);
        Assert.Equal(3, executor.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsProgressFromRunner()
    {
        var executor = new BenchmarkTestDoubles.SequenceIterationExecutor([7, 11]);
        var runner = new BenchmarkRunner(executor);
        var service = new BenchmarkExecutionService(runner);
        int progressEvents = 0;

        await service.ExecuteAsync(
            runLabel: "R2",
            configuration: new BenchmarkRunConfiguration(Iterations: 2, WarmupIterations: 0, IntervalMs: 0),
            onProgress: _ => progressEvents++,
            cancellationToken: CancellationToken.None);

        Assert.Equal(2, progressEvents);
    }
}


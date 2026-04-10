namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkRunner(
    IBenchmarkIterationExecutor iterationExecutor,
    IBenchmarkDelayScheduler? delayScheduler = null) : IBenchmarkRunner
{
    private readonly IBenchmarkIterationExecutor _iterationExecutor = iterationExecutor;
    private readonly IBenchmarkDelayScheduler _delayScheduler = delayScheduler ?? new TaskDelayScheduler();

    public async Task<IReadOnlyList<double>> RunAsync(
        BenchmarkRunConfiguration configuration,
        Action<BenchmarkRunProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        BenchmarkRunConfiguration config = configuration.Normalize();
        int total = config.WarmupIterations + config.Iterations;
        var latencies = new List<double>(config.Iterations);

        for (int i = 0; i < config.WarmupIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onProgress?.Invoke(new BenchmarkRunProgress(BenchmarkRunStage.Warmup, i + 1, total, null));
            await _iterationExecutor.ExecuteIterationAsync(cancellationToken);
            await DelayIfRequiredAsync(config.IntervalMs, cancellationToken);
        }

        for (int i = 0; i < config.Iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double ms = await _iterationExecutor.ExecuteIterationAsync(cancellationToken);
            latencies.Add(ms);
            onProgress?.Invoke(new BenchmarkRunProgress(BenchmarkRunStage.Iteration, config.WarmupIterations + i + 1, total, ms));
            await DelayIfRequiredAsync(config.IntervalMs, cancellationToken);
        }

        return latencies;
    }

    private Task DelayIfRequiredAsync(int intervalMs, CancellationToken cancellationToken)
    {
        if (intervalMs <= 0)
            return Task.CompletedTask;

        return _delayScheduler.DelayAsync(intervalMs, cancellationToken);
    }
}


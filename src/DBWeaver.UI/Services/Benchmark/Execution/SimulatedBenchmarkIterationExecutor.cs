namespace DBWeaver.UI.Services.Benchmark;

public sealed class SimulatedBenchmarkIterationExecutor : IBenchmarkIterationExecutor
{
    private readonly IBenchmarkLatencyProfileSampler _latencyProfileSampler;
    private readonly IBenchmarkDelayScheduler _delayScheduler;
    private readonly Random _random;

    public SimulatedBenchmarkIterationExecutor(
        IBenchmarkLatencyProfileSampler? latencyProfileSampler = null,
        IBenchmarkDelayScheduler? delayScheduler = null,
        Random? random = null)
    {
        _latencyProfileSampler = latencyProfileSampler ?? new BenchmarkLatencyProfileSampler();
        _delayScheduler = delayScheduler ?? new TaskDelayScheduler();
        _random = random ?? Random.Shared;
    }

    public async Task<double> ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        // Simulated latency profile uses a base bucket with occasional spikes.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int delay = _latencyProfileSampler.SampleDelayMs(_random);
        await _delayScheduler.DelayAsync(delay, cancellationToken);
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }
}


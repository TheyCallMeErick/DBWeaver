using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.Benchmark;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DBWeaver.Tests.Unit.ViewModels;

/// <summary>
/// Tests for BenchmarkViewModel thread safety using Random.Shared.
/// Regression tests for: Static Random not thread-safe, causing race conditions
/// in SimulateIterationAsync during concurrent benchmark iterations.
/// </summary>
public class BenchmarkViewModelThreadSafetyTests
{
    private static readonly SimulatedBenchmarkIterationExecutor _executor = new();

    [Fact]
    public void BenchmarkViewModel_CanBeInstantiated()
    {
        var canvas = new CanvasViewModel();
        var vm = new BenchmarkViewModel(canvas);
        Assert.NotNull(vm);
    }

    [Fact]
    public async Task SimulateIterationAsync_ReturnsPositiveLatency()
    {
        var ct = CancellationToken.None;
        var latency = await _executor.ExecuteIterationAsync(ct);
        Assert.True(latency > 0);
    }

    [Fact]
    public async Task SimulateIterationAsync_MultipleCallsProduceDifferentValues()
    {
        var ct = CancellationToken.None;
        var latencies = new List<double>();

        for (int i = 0; i < 5; i++)
        {
            var latency = await _executor.ExecuteIterationAsync(ct);
            latencies.Add(latency);
        }

        var distinct = latencies.Distinct().Count();
        Assert.True(distinct >= 2, "Expected varied latencies using Random.Shared");
    }

    [Fact]
    public async Task SimulateIterationAsync_ThreadSafe_Concurrent()
    {
        var ct = CancellationToken.None;
        var results = new List<double>();
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                var latency = await _executor.ExecuteIterationAsync(ct);
                lock (lockObj)
                    results.Add(latency);
            })
            .ToList();

        await Task.WhenAll(tasks);

        Assert.Equal(20, results.Count);
        Assert.True(results.All(l => l > 0));
    }

    [Fact]
    public async Task SimulateIterationAsync_AllLatenciesValid()
    {
        var ct = CancellationToken.None;

        for (int i = 0; i < 10; i++)
        {
            var latency = await _executor.ExecuteIterationAsync(ct);
            // Simulated delay is 30-80ms with occasional 200-600ms spikes.
            // In CI, scheduler jitter can add substantial overhead, so we validate
            // a broad but still bounded envelope instead of brittle tight buckets.
            bool valid = latency is >= 20 and <= 800;
            Assert.True(valid, $"Latency {latency} outside expected ranges");
        }
    }

    [Fact]
    public async Task SimulateIterationAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should throw TaskCanceledException (which is a subclass of OperationCanceledException)
        // Task.Delay with a cancelled token throws TaskCanceledException, not OperationCanceledException
        var ex = await Assert.ThrowsAsync<TaskCanceledException>(
            () => _executor.ExecuteIterationAsync(cts.Token)
        );
        Assert.NotNull(ex);
    }

    [Fact]
    public async Task SimulateIterationAsync_HighConcurrency_NoDeadlock()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var results = new List<double>();
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 50)
            .Select(async _ =>
            {
                var latency = await _executor.ExecuteIterationAsync(cts.Token);
                lock (lockObj)
                    results.Add(latency);
            })
            .ToList();

        await Task.WhenAll(tasks);

        Assert.Equal(50, results.Count);
    }

    [Fact]
    public async Task RegressionTest_RandomShared_ThreadSafe()
    {
        // Primary regression test: Random.Shared is thread-safe
        // Before: static Random _rng = new() - NOT thread-safe
        // After: Random.Shared - thread-safe reference type

        var ct = CancellationToken.None;
        int successCount = 0;

        var tasks = Enumerable.Range(0, 100)
            .Select(async _ =>
            {
                try
                {
                    var latency = await _executor.ExecuteIterationAsync(ct);
                    if (latency > 0)
                        Interlocked.Increment(ref successCount);
                }
                catch
                {
                    // Should not throw with Random.Shared
                    throw;
                }
            })
            .ToList();

        await Task.WhenAll(tasks);
        Assert.Equal(100, successCount);
    }

    [Fact]
    public async Task SimulateIterationAsync_LatencyMeasurementReasonable()
    {
        var ct = CancellationToken.None;

        for (int i = 0; i < 3; i++)
        {
            var before = DateTime.UtcNow;
            var latency = await _executor.ExecuteIterationAsync(ct);
            var after = DateTime.UtcNow;
            var elapsed = (after - before).TotalMilliseconds;

            // Measured should be reasonably close to actual (within 100ms margin for test overhead)
            var diff = Math.Abs(latency - elapsed);
            Assert.True(diff < 150, $"Large diff: measured {latency}ms vs actual {elapsed}ms");
        }
    }

    [Fact]
    public async Task SimulateIterationAsync_RepeatedCalls_NoAccumulation()
    {
        // Regression: Verify no handler accumulation with repeated calls
        var ct = CancellationToken.None;

        for (int iteration = 0; iteration < 3; iteration++)
        {
            var latencies = new List<double>();
            for (int i = 0; i < 10; i++)
            {
                var latency = await _executor.ExecuteIterationAsync(ct);
                latencies.Add(latency);
            }
            Assert.Equal(10, latencies.Count);
        }
    }
}

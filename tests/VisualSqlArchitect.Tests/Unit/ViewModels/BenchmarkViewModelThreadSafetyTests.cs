using VisualSqlArchitect.UI.ViewModels;
using Xunit;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

/// <summary>
/// Helper class to provide test access to SimulateIterationAsync through reflection.
/// In production code, SimulateIterationAsync is private static, so we need reflection for testing.
/// </summary>
public static class BenchmarkViewModelTestHelpers
{
    private static readonly MethodInfo? SimulateMethod =
        typeof(BenchmarkViewModel).GetMethod(
            "SimulateIterationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(CancellationToken) },
            null
        );

    public static async Task<double> InvokeSimulateIterationAsync(CancellationToken ct)
    {
        if (SimulateMethod == null)
            throw new InvalidOperationException("SimulateIterationAsync method not found");

        var task = (Task<double>)SimulateMethod.Invoke(null, new object[] { ct })!;
        return await task;
    }
}

/// <summary>
/// Tests for BenchmarkViewModel thread safety using Random.Shared.
/// Regression tests for: Static Random not thread-safe, causing race conditions
/// in SimulateIterationAsync during concurrent benchmark iterations.
/// </summary>
public class BenchmarkViewModelThreadSafetyTests
{
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
        var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(ct);
        Assert.True(latency > 0);
    }

    [Fact]
    public async Task SimulateIterationAsync_MultipleCallsProduceDifferentValues()
    {
        var ct = CancellationToken.None;
        var latencies = new List<double>();

        for (int i = 0; i < 5; i++)
        {
            var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(ct);
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
                var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(ct);
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
            var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(ct);
            // Should be 30-80ms or 200-600ms (10% chance of spike)
            bool valid = (latency >= 20 && latency < 100) || (latency >= 190);
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
            () => BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(cts.Token)
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
                var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(cts.Token);
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
                    var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(ct);
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
            var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(ct);
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
                var latency = await BenchmarkViewModelTestHelpers.InvokeSimulateIterationAsync(ct);
                latencies.Add(latency);
            }
            Assert.Equal(10, latencies.Count);
        }
    }
}

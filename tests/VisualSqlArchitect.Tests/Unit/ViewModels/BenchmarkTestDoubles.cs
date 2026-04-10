
using DBWeaver.UI.Services.Benchmark;

namespace DBWeaver.Tests.Unit.ViewModels;

internal static class BenchmarkTestDoubles
{
    internal sealed class SequenceIterationExecutor(IEnumerable<double> sequence) : IBenchmarkIterationExecutor
    {
        private readonly Queue<double> _sequence = new(sequence);

        public int ExecutionCount { get; private set; }

        public Task<double> ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            if (_sequence.Count == 0)
                return Task.FromResult(42d);
            return Task.FromResult(_sequence.Dequeue());
        }
    }

    internal sealed class FixedBenchmarkConfigurationProvider(BenchmarkRunConfiguration config) : IBenchmarkConfigurationProvider
    {
        public BenchmarkRunConfiguration GetDefaultConfiguration() => config;
    }

    internal sealed class FixedInitializationService(BenchmarkInitialState state) : IBenchmarkInitializationService
    {
        public BenchmarkInitialState BuildInitialState() => state;
    }

    internal sealed class FixedTextProvider(string defaultLabel) : IBenchmarkTextProvider
    {
        public string DefaultRunLabel => defaultLabel;
        public string BuildRunLabel(int runNumber) => $"LBL#{runNumber}";
        public string NoSqlToBenchmark() => "NO_SQL";
        public string WarmupProgress(int completed, int warmupTotal) => $"W:{completed}/{warmupTotal}";
        public string IterationProgress(int completed, int iterationTotal) => $"I:{completed}/{iterationTotal}";
        public string Completed(string summary) => $"DONE:{summary}";
        public string Cancelled() => "CANCEL";
        public string FailedWithReason(string reason) => $"FAIL:{reason}";
    }

    internal sealed class FakeBenchmarkTextProvider : IBenchmarkTextProvider
    {
        public string DefaultRunLabel => "RUN";
        public string BuildRunLabel(int runNumber) => $"RUN#{runNumber}";
        public string NoSqlToBenchmark() => "NO_SQL";
        public string WarmupProgress(int completed, int warmupTotal) => $"WARMUP:{completed}/{warmupTotal}";
        public string IterationProgress(int completed, int iterationTotal) => $"ITER:{completed}/{iterationTotal}";
        public string Completed(string summary) => $"DONE:{summary}";
        public string Cancelled() => "CANCELLED";
        public string FailedWithReason(string reason) => $"FAILED:{reason}";
    }

    internal sealed class RecordingDelayScheduler : IBenchmarkDelayScheduler
    {
        public int CallCount { get; private set; }
        public List<int> Milliseconds { get; } = [];

        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Milliseconds.Add(milliseconds);
            return Task.CompletedTask;
        }
    }

    internal sealed class FixedRunContextFactory(string sql) : IBenchmarkRunContextFactory
    {
        public BenchmarkRunContextCreationResult TryCreate(
            string rawSql,
            int iterations,
            int warmupIterations,
            int intervalMs)
        {
            var config = new BenchmarkRunConfiguration(iterations, warmupIterations, intervalMs).Normalize();
            var context = new BenchmarkRunContext(sql, config, new CancellationTokenSource());
            return new BenchmarkRunContextCreationResult(context, RejectionMessage: null);
        }
    }

    internal sealed class RejectingRunContextFactory(string message) : IBenchmarkRunContextFactory
    {
        public BenchmarkRunContextCreationResult TryCreate(
            string rawSql,
            int iterations,
            int warmupIterations,
            int intervalMs) =>
            new(Context: null, RejectionMessage: message);
    }
}

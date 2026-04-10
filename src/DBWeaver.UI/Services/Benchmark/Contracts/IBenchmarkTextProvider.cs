namespace DBWeaver.UI.Services.Benchmark;

public interface IBenchmarkTextProvider
{
    string DefaultRunLabel { get; }

    string BuildRunLabel(int runNumber);

    string NoSqlToBenchmark();

    string WarmupProgress(int completed, int warmupTotal);

    string IterationProgress(int completed, int iterationTotal);

    string Completed(string summary);

    string Cancelled();

    string FailedWithReason(string reason);
}


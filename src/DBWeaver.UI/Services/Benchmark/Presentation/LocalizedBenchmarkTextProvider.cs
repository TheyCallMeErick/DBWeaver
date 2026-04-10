using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.Benchmark;

public sealed class LocalizedBenchmarkTextProvider : IBenchmarkTextProvider
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public string DefaultRunLabel => L("benchmark.runLabelDefault", "Run");

    public string BuildRunLabel(int runNumber) =>
        string.Format(L("benchmark.runLabelPattern", "Run {0}"), runNumber);

    public string NoSqlToBenchmark() =>
        L("benchmark.status.noSql", "No SQL to benchmark - build a query first.");

    public string WarmupProgress(int completed, int warmupTotal) =>
        string.Format(L("benchmark.status.warmupProgress", "Warm-up {0}/{1}..."), completed, warmupTotal);

    public string IterationProgress(int completed, int iterationTotal) =>
        string.Format(L("benchmark.status.iterationProgress", "Iteration {0}/{1}..."), completed, iterationTotal);

    public string Completed(string summary) =>
        string.Format(L("benchmark.status.done", "Done - {0}"), summary);

    public string Cancelled() =>
        L("benchmark.status.cancelled", "Benchmark cancelled.");

    public string FailedWithReason(string reason) =>
        string.Format(L("benchmark.status.failedWithReason", "Benchmark failed: {0}"), reason);

    private string L(string key, string fallback)
    {
        string value = _loc[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}


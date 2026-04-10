namespace DBWeaver.UI.Services.Benchmark;

public static class BenchmarkStatisticsCalculator
{
    public static BenchmarkRunResult BuildResult(
        string label,
        int iterations,
        IEnumerable<double> latencies,
        DateTime runAt)
    {
        List<double> sorted = latencies.OrderBy(x => x).ToList();
        if (sorted.Count == 0)
            throw new ArgumentException("At least one latency sample is required.", nameof(latencies));

        return new BenchmarkRunResult(
            Label: label,
            Iterations: iterations,
            MinMs: sorted[0],
            MaxMs: sorted[^1],
            AvgMs: sorted.Average(),
            MedianMs: Percentile(sorted, 0.50),
            P95Ms: Percentile(sorted, 0.95),
            RunAt: runAt
        );
    }

    public static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0)
            return 0;
        if (sorted.Count == 1)
            return sorted[0];

        double idx = p * (sorted.Count - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, sorted.Count - 1);
        double frac = idx - lo;
        return sorted[lo] + frac * (sorted[hi] - sorted[lo]);
    }
}


namespace DBWeaver.UI.Services.Benchmark;

public sealed record BenchmarkRunResult(
    string Label,
    int Iterations,
    double MinMs,
    double MaxMs,
    double AvgMs,
    double MedianMs,
    double P95Ms,
    DateTime RunAt)
{
    public string Summary =>
        $"avg {AvgMs:0.0}ms  p95 {P95Ms:0.0}ms  min {MinMs:0.0}ms  max {MaxMs:0.0}ms";
}

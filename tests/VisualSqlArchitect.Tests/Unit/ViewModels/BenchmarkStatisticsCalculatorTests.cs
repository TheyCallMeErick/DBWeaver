using DBWeaver.UI.Services.Benchmark;


namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkStatisticsCalculatorTests
{
    [Fact]
    public void Percentile_ReturnsZero_ForEmptySeries()
    {
        double value = BenchmarkStatisticsCalculator.Percentile([], 0.95);
        Assert.Equal(0, value);
    }

    [Fact]
    public void BuildResult_ComputesExpectedMetrics()
    {
        double[] latencies = [10, 20, 30, 40, 50];

        BenchmarkRunResult result = BenchmarkStatisticsCalculator.BuildResult(
            label: "Run X",
            iterations: 5,
            latencies: latencies,
            runAt: new DateTime(2026, 1, 1)
        );

        Assert.Equal(10, result.MinMs);
        Assert.Equal(50, result.MaxMs);
        Assert.Equal(30, result.AvgMs);
        Assert.Equal(30, result.MedianMs);
        Assert.InRange(result.P95Ms, 47.0, 50.0);
    }

    [Fact]
    public void BuildResult_Throws_ForEmptySeries()
    {
        Assert.Throws<ArgumentException>(() =>
            BenchmarkStatisticsCalculator.BuildResult(
                label: "Run Empty",
                iterations: 0,
                latencies: [],
                runAt: DateTime.Now));
    }
}


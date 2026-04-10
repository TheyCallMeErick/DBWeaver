using DBWeaver.UI.Services.Benchmark;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkRunContextFactoryTests
{
    [Fact]
    public void TryCreate_ReturnsRejection_WhenSqlIsMissing()
    {
        var factory = new BenchmarkRunContextFactory(new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        BenchmarkRunContextCreationResult result = factory.TryCreate(
            rawSql: "   ",
            iterations: 10,
            warmupIterations: 1,
            intervalMs: 50);

        Assert.False(result.CanStart);
        Assert.Null(result.Context);
        Assert.Equal("NO_SQL", result.RejectionMessage);
    }

    [Fact]
    public void TryCreate_ReturnsContext_WhenSqlIsPresent()
    {
        var factory = new BenchmarkRunContextFactory(new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        BenchmarkRunContextCreationResult result = factory.TryCreate(
            rawSql: "SELECT 1",
            iterations: 12,
            warmupIterations: 2,
            intervalMs: 150);

        Assert.True(result.CanStart);
        Assert.NotNull(result.Context);
        Assert.Null(result.RejectionMessage);

        BenchmarkRunContext context = result.Context!.Value;
        Assert.Equal("SELECT 1", context.Sql);
        Assert.Equal(12, context.Configuration.Iterations);
        Assert.Equal(2, context.Configuration.WarmupIterations);
        Assert.Equal(150, context.Configuration.IntervalMs);

        context.CancellationTokenSource.Dispose();
    }

    [Fact]
    public void TryCreate_NormalizesConfigurationValues()
    {
        var factory = new BenchmarkRunContextFactory(new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        BenchmarkRunContextCreationResult result = factory.TryCreate(
            rawSql: "SELECT 1",
            iterations: -1,
            warmupIterations: -3,
            intervalMs: -99);

        BenchmarkRunContext context = result.Context!.Value;
        Assert.Equal(BenchmarkRunConfiguration.NormalizeIterations(-1), context.Configuration.Iterations);
        Assert.Equal(BenchmarkRunConfiguration.NormalizeWarmupIterations(-3), context.Configuration.WarmupIterations);
        Assert.Equal(BenchmarkRunConfiguration.NormalizeIntervalMs(-99), context.Configuration.IntervalMs);

        context.CancellationTokenSource.Dispose();
    }
}


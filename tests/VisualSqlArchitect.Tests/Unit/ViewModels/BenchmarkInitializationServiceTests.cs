using DBWeaver.UI.Services.Benchmark;


namespace DBWeaver.Tests.Unit.ViewModels;

public sealed class BenchmarkInitializationServiceTests
{
    [Fact]
    public void BuildInitialState_UsesNormalizedConfigAndDefaultLabel()
    {
        var configProvider = new BenchmarkTestDoubles.FixedBenchmarkConfigurationProvider(new BenchmarkRunConfiguration(999, -3, 9000));
        var textProvider = new BenchmarkTestDoubles.FixedTextProvider("RUN_DEFAULT");
        var service = new BenchmarkInitializationService(configProvider, textProvider);

        BenchmarkInitialState state = service.BuildInitialState();

        Assert.Equal(100, state.Iterations);
        Assert.Equal(0, state.WarmupIterations);
        Assert.Equal(5000, state.IntervalMs);
        Assert.Equal("RUN_DEFAULT", state.RunLabel);
    }
}


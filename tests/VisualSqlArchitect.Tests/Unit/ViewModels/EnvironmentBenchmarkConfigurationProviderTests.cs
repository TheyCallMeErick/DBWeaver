using DBWeaver.UI.Services.Benchmark;


namespace DBWeaver.Tests.Unit.ViewModels;

public sealed class EnvironmentBenchmarkConfigurationProviderTests
{
    [Fact]
    public void GetDefaultConfiguration_UsesFallbacks_WhenVariablesMissing()
    {
        using var scope = new EnvScope(
            (EnvironmentBenchmarkConfigurationProvider.IterationsEnvVar, null),
            (EnvironmentBenchmarkConfigurationProvider.WarmupEnvVar, null),
            (EnvironmentBenchmarkConfigurationProvider.IntervalEnvVar, null)
        );

        var provider = new EnvironmentBenchmarkConfigurationProvider();
        BenchmarkRunConfiguration config = provider.GetDefaultConfiguration();

        Assert.Equal(10, config.Iterations);
        Assert.Equal(2, config.WarmupIterations);
        Assert.Equal(0, config.IntervalMs);
    }

    [Fact]
    public void GetDefaultConfiguration_ParsesAndClampsValues()
    {
        using var scope = new EnvScope(
            (EnvironmentBenchmarkConfigurationProvider.IterationsEnvVar, "999"),
            (EnvironmentBenchmarkConfigurationProvider.WarmupEnvVar, "-5"),
            (EnvironmentBenchmarkConfigurationProvider.IntervalEnvVar, "7000")
        );

        var provider = new EnvironmentBenchmarkConfigurationProvider();
        BenchmarkRunConfiguration config = provider.GetDefaultConfiguration();

        Assert.Equal(100, config.Iterations);
        Assert.Equal(0, config.WarmupIterations);
        Assert.Equal(5000, config.IntervalMs);
    }

    [Fact]
    public void GetDefaultConfiguration_UsesFallbacks_WhenValuesInvalid()
    {
        using var scope = new EnvScope(
            (EnvironmentBenchmarkConfigurationProvider.IterationsEnvVar, "abc"),
            (EnvironmentBenchmarkConfigurationProvider.WarmupEnvVar, "x"),
            (EnvironmentBenchmarkConfigurationProvider.IntervalEnvVar, "nan")
        );

        var provider = new EnvironmentBenchmarkConfigurationProvider();
        BenchmarkRunConfiguration config = provider.GetDefaultConfiguration();

        Assert.Equal(10, config.Iterations);
        Assert.Equal(2, config.WarmupIterations);
        Assert.Equal(0, config.IntervalMs);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly List<(string Name, string? Previous)> _previous = [];

        public EnvScope(params (string Name, string? Value)[] values)
        {
            foreach ((string name, string? value) in values)
            {
                _previous.Add((name, Environment.GetEnvironmentVariable(name)));
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach ((string name, string? previous) in _previous)
                Environment.SetEnvironmentVariable(name, previous);
        }
    }
}


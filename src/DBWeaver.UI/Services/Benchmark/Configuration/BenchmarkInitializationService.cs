namespace DBWeaver.UI.Services.Benchmark;

public sealed class BenchmarkInitializationService(
    IBenchmarkConfigurationProvider configurationProvider,
    IBenchmarkTextProvider textProvider) : IBenchmarkInitializationService
{
    private readonly IBenchmarkConfigurationProvider _configurationProvider = configurationProvider;
    private readonly IBenchmarkTextProvider _textProvider = textProvider;

    public BenchmarkInitialState BuildInitialState()
    {
        BenchmarkRunConfiguration defaults = _configurationProvider.GetDefaultConfiguration().Normalize();
        return new BenchmarkInitialState(
            Iterations: defaults.Iterations,
            WarmupIterations: defaults.WarmupIterations,
            IntervalMs: defaults.IntervalMs,
            RunLabel: _textProvider.DefaultRunLabel
        );
    }
}


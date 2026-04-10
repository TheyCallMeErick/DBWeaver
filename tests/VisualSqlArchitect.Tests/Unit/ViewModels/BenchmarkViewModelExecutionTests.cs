using DBWeaver.UI.Services.Benchmark;


namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkViewModelExecutionTests
{
    [Fact]
    public void Ctor_UsesConfigurationProviderDefaults()
    {
        var canvas = new CanvasViewModel();
        var configProvider = new BenchmarkTestDoubles.FixedBenchmarkConfigurationProvider(new BenchmarkRunConfiguration(33, 4, 123));

        var vm = new BenchmarkViewModel(canvas, configurationProvider: configProvider);

        Assert.Equal(33, vm.Iterations);
        Assert.Equal(4, vm.WarmupIterations);
        Assert.Equal(123, vm.IntervalMs);
    }

    [Fact]
    public void Ctor_UsesInjectedInitializationService_WhenProvided()
    {
        var canvas = new CanvasViewModel();
        var init = new BenchmarkTestDoubles.FixedInitializationService(new BenchmarkInitialState(7, 1, 55, "INIT_LABEL"));

        var vm = new BenchmarkViewModel(canvas, initializationService: init);

        Assert.Equal(7, vm.Iterations);
        Assert.Equal(1, vm.WarmupIterations);
        Assert.Equal(55, vm.IntervalMs);
        Assert.Equal("INIT_LABEL", vm.RunLabel);
    }

    [Fact]
    public void Open_UsesTextProviderRunLabelPattern()
    {
        var canvas = new CanvasViewModel();
        var vm = new BenchmarkViewModel(
            canvas,
            textProvider: new BenchmarkTestDoubles.FakeBenchmarkTextProvider());

        vm.Open();

        Assert.Equal("RUN#1", vm.RunLabel);
    }

    [Fact]
    public async Task RunAsync_UsesInjectedExecutorAndStoresResult()
    {
        var canvas = new CanvasViewModel();
        var executor = new BenchmarkTestDoubles.SequenceIterationExecutor([11, 13, 17]);
        var runner = new BenchmarkRunner(executor);
        var vm = new BenchmarkViewModel(
            canvas,
            iterationExecutor: executor,
            benchmarkRunner: runner,
            runContextFactory: new BenchmarkTestDoubles.FixedRunContextFactory("SELECT 1"),
            textProvider: new BenchmarkTestDoubles.FakeBenchmarkTextProvider())
        {
            Iterations = 3,
            WarmupIterations = 0,
            IntervalMs = 0,
            RunLabel = "Deterministic"
        };

        vm.RunCommand.Execute(null);
        await WaitUntilAsync(() => vm.LatestResult is not null || !vm.IsRunning);

        Assert.NotNull(vm.LatestResult);
        Assert.Equal(3, vm.LatestResult.Iterations);
        Assert.Equal(11, vm.LatestResult.MinMs);
        Assert.Equal(17, vm.LatestResult.MaxMs);
        Assert.Equal(3, executor.ExecutionCount);
        Assert.Single(vm.History);
        Assert.Contains("DONE:", vm.Progress, StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 2000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;

            await Task.Delay(10);
        }

        Assert.True(predicate(), "Timed out waiting for benchmark completion");
    }
}


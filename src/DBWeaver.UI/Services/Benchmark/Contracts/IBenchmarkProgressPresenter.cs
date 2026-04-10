namespace DBWeaver.UI.Services.Benchmark;

public interface IBenchmarkProgressPresenter
{
    BenchmarkProgressViewState Build(
        BenchmarkRunProgress progress,
        int warmupIterations,
        int iterations);
}


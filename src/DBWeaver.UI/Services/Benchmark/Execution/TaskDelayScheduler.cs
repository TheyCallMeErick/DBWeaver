namespace DBWeaver.UI.Services.Benchmark;

public sealed class TaskDelayScheduler : IBenchmarkDelayScheduler
{
    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken) =>
        Task.Delay(milliseconds, cancellationToken);
}


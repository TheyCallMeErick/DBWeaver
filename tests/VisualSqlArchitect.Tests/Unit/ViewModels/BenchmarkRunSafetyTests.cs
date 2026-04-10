using DBWeaver.UI.Services.Benchmark;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class BenchmarkRunSafetyTests
{
    [Fact]
    public async Task RunAsyncSafe_DoesNotThrow_WhenNoSqlIsAvailable()
    {
        var canvas = new CanvasViewModel();
        var vm = new BenchmarkViewModel(
            canvas,
            runContextFactory: new BenchmarkTestDoubles.RejectingRunContextFactory("NO_SQL"));

        vm.RunCommand.Execute(null);
        await WaitUntilAsync(() => !string.IsNullOrWhiteSpace(vm.Progress));

        Assert.Equal("NO_SQL", vm.Progress);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void BenchmarkViewModel_RunAndCancelCommands_AreInitialized()
    {
        var vm = new BenchmarkViewModel(new CanvasViewModel());

        Assert.NotNull(vm.RunCommand);
        Assert.NotNull(vm.CancelCommand);
        Assert.True(vm.RunCommand.CanExecute(null));
        Assert.False(vm.CancelCommand.CanExecute(null));
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

        Assert.True(predicate(), "Timed out waiting for benchmark progress update");
    }
}


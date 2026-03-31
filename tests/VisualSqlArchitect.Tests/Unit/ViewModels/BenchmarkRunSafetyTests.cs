using System.Reflection;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class BenchmarkRunSafetyTests
{
    [Fact]
    public async Task RunAsyncSafe_DoesNotThrow_WhenNoSqlIsAvailable()
    {
        var canvas = new CanvasViewModel();
        var vm = new BenchmarkViewModel(canvas);

        MethodInfo method = typeof(BenchmarkViewModel)
            .GetMethod("RunAsyncSafe", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Task task = (Task)method.Invoke(vm, null)!;
        await task;

        Assert.False(string.IsNullOrWhiteSpace(vm.Progress));
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void BenchmarkViewModel_HasSafeRunLauncher()
    {
        Assert.NotNull(typeof(BenchmarkViewModel)
            .GetMethod("StartRunSafe", BindingFlags.Instance | BindingFlags.NonPublic));
    }
}

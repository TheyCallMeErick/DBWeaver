using System.Reflection;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class ConnectionManagerFireAndForgetSafetyTests
{
    [Fact]
    public async Task ExecuteFireAndForgetSafeAsync_CatchesUnhandledExceptions()
    {
        var vm = new ConnectionManagerViewModel();
        MethodInfo method = typeof(ConnectionManagerViewModel)
            .GetMethod("ExecuteFireAndForgetSafeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Func<Task> throwing = () => Task.FromException(new InvalidOperationException("boom"));

        Task task = (Task)method.Invoke(vm, [throwing, "unit-test-op"])!;
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteFireAndForgetSafeAsync_IgnoresOperationCanceled()
    {
        var vm = new ConnectionManagerViewModel();
        MethodInfo method = typeof(ConnectionManagerViewModel)
            .GetMethod("ExecuteFireAndForgetSafeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Func<Task> canceled = () => Task.FromCanceled(new CancellationToken(canceled: true));

        Task task = (Task)method.Invoke(vm, [canceled, "unit-test-op"])!;
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void ConnectionManager_HasSafeStarterMethodsForFireAndForgetFlows()
    {
        Type t = typeof(ConnectionManagerViewModel);

        Assert.NotNull(t.GetMethod("StartTestConnectionSafe", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(t.GetMethod("StartRefreshHealthSafe", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(t.GetMethod("StartLoadDatabaseTablesSafe", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(t.GetMethod("StartHealthMonitorLoopSafe", BindingFlags.Instance | BindingFlags.NonPublic));
    }
}

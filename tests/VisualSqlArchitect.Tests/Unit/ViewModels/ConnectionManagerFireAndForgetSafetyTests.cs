using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionManagerFireAndForgetSafetyTests
{
    [Fact]
    public async Task FireAndForgetSafetyExecutor_CatchesUnhandledExceptions()
    {
        var executor = new FireAndForgetSafetyExecutor(NullLogger.Instance);

        Func<Task> throwing = () => Task.FromException(new InvalidOperationException("boom"));

        Task task = executor.ExecuteSafeAsync(throwing, "unit-test-op");
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task FireAndForgetSafetyExecutor_IgnoresOperationCanceled()
    {
        var executor = new FireAndForgetSafetyExecutor(NullLogger.Instance);

        Func<Task> canceled = () => Task.FromCanceled(new CancellationToken(canceled: true));

        Task task = executor.ExecuteSafeAsync(canceled, "unit-test-op");
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void ConnectionManager_RefreshHealthCommand_UsesSafeExecutor()
    {
        var fake = new FakeFireAndForgetSafetyExecutor();
        var vm = new ConnectionManagerViewModel(fireAndForgetSafetyExecutor: fake)
        {
            ActiveProfileId = "p-1"
        };

        vm.RefreshHealthCommand.Execute(null);

        Assert.True(fake.CallCount >= 2);
        Assert.Contains("refresh health", fake.OperationNames);
    }

    private sealed class FakeFireAndForgetSafetyExecutor : IFireAndForgetSafetyExecutor
    {
        public int CallCount { get; private set; }
        public List<string> OperationNames { get; } = [];

        public Task ExecuteSafeAsync(Func<Task> operation, string operationName)
        {
            CallCount++;
            OperationNames.Add(operationName);
            return Task.CompletedTask;
        }
    }
}



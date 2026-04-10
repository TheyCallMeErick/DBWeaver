using DBWeaver.UI.Services.Benchmark;


namespace DBWeaver.Tests.Unit.ViewModels;

public sealed class BenchmarkCommandFactoryTests
{
    [Fact]
    public void Create_BuildsCommandsWithExpectedCanExecuteAndActions()
    {
        bool canRun = true;
        bool canCancel = false;
        int runCalls = 0;
        int cancelCalls = 0;
        int clearCalls = 0;
        int closeCalls = 0;

        BenchmarkCommandBindings bindings = BenchmarkCommandFactory.Create(
            startRun: () => runCalls++,
            canRun: () => canRun,
            cancel: () => cancelCalls++,
            canCancel: () => canCancel,
            clearHistory: () => clearCalls++,
            close: () => closeCalls++);

        Assert.True(bindings.RunCommand.CanExecute(null));
        Assert.False(bindings.CancelCommand.CanExecute(null));

        bindings.RunCommand.Execute(null);
        bindings.ClearHistoryCommand.Execute(null);
        bindings.CloseCommand.Execute(null);

        Assert.Equal(1, runCalls);
        Assert.Equal(1, clearCalls);
        Assert.Equal(1, closeCalls);
        Assert.Equal(0, cancelCalls);

        canRun = false;
        canCancel = true;

        Assert.False(bindings.RunCommand.CanExecute(null));
        Assert.True(bindings.CancelCommand.CanExecute(null));

        bindings.CancelCommand.Execute(null);
        Assert.Equal(1, cancelCalls);
    }
}


using System.Diagnostics;

using Xunit;

namespace DBWeaver.Tests.Unit.Performance;

public class StartupPerformanceSmokeTests
{
    [Fact]
    public void ShellInitialization_StaysWithinStartupBudget()
    {
        var sw = Stopwatch.StartNew();
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        sw.Stop();

        Assert.NotNull(shell);
        Assert.True(sw.ElapsedMilliseconds < 1500,
            $"Shell startup exceeded baseline budget: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void FirstCanvasInitialization_StaysWithinBaselineBudget()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        var sw = Stopwatch.StartNew();
        shell.EnterCanvas();
        sw.Stop();

        Assert.NotNull(shell.Canvas);
        Assert.True(sw.ElapsedMilliseconds < 4000,
            $"First canvas initialization exceeded baseline budget: {sw.ElapsedMilliseconds}ms");
    }
}

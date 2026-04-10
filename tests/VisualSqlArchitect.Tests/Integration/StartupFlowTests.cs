
using Xunit;

namespace Integration;

public class StartupFlowTests
{
    [Fact]
    public void Startup_DefaultShell_OpensInStartModeWithoutCanvas()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        Assert.True(shell.IsStartVisible);
        Assert.False(shell.IsCanvasVisible);
        Assert.Null(shell.Canvas);
    }

    [Fact]
    public void Startup_WhenEnteringCanvas_CreatesCanvasAndKeepsSingleInstance()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        shell.EnterCanvas();
        var first = shell.Canvas;
        shell.ReturnToStart();
        shell.EnterCanvas();
        var second = shell.Canvas;

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.True(shell.IsCanvasVisible);
        Assert.False(shell.IsStartVisible);
    }
}

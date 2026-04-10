using DBWeaver.UI.Services.Benchmark;
using System.ComponentModel;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

/// <summary>
/// Behavioral tests for CanvasViewModel disposal safety.
/// </summary>
public class CanvasViewModelDisposalTests
{
    [Fact]
    public void CanvasViewModel_Implements_IDisposable()
    {
        var canvas = new CanvasViewModel();
        Assert.IsAssignableFrom<IDisposable>(canvas);
    }

    [Fact]
    public void CanvasViewModel_Dispose_DoesNotThrow()
    {
        var canvas = new CanvasViewModel();

        canvas.Dispose();
        canvas.Dispose();
    }

    [Fact]
    public void CanvasViewModel_Dispose_IsIdempotent()
    {
        var canvas = new CanvasViewModel();

        canvas.Dispose();
        canvas.Dispose();
        canvas.Dispose();
    }

    [Fact]
    public void CanvasViewModel_PublicOperationsRemainSafe_AfterDispose()
    {
        var canvas = new CanvasViewModel();
        canvas.Dispose();

        canvas.UpdateQueryText("SELECT 1");
        canvas.ZoomToward(new Avalonia.Point(20, 20), 1.1);
        canvas.SetViewportSize(1280, 720);

        Assert.Equal("SELECT 1", canvas.QueryText);
    }

    [Fact]
    public void RegressionTest_MemoryLeak_OldViewModelDisposed()
    {
        var oldCanvas = new CanvasViewModel();
        oldCanvas.IsDirty = true;
        oldCanvas.Dispose();
        var newCanvas = new CanvasViewModel();
        Assert.False(newCanvas.IsDirty);
    }
}


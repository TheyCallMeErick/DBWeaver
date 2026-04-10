using DBWeaver.UI.Services;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="ActiveCanvasProvider"/> and the <see cref="IActiveCanvasProvider"/> contract.
/// </summary>
public class ActiveCanvasProviderTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenResolveIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new ActiveCanvasProvider(null!)
        );

        Assert.Equal("resolve", ex.ParamName);
    }

    [Fact]
    public void GetActive_ReturnsCanavasFromDelegate()
    {
        var canvas = new CanvasViewModel();
        var provider = new ActiveCanvasProvider(() => canvas);

        CanvasViewModel result = provider.GetActive();

        Assert.Same(canvas, result);
    }

    [Fact]
    public void GetActive_InvokesDelegateOnEveryCall()
    {
        int callCount = 0;
        var canvas = new CanvasViewModel();
        var provider = new ActiveCanvasProvider(() =>
        {
            callCount++;
            return canvas;
        });

        provider.GetActive();
        provider.GetActive();
        provider.GetActive();

        Assert.Equal(3, callCount);
    }

    [Fact]
    public void GetActive_ReturnsLatestCanvas_WhenDelegateChangesResult()
    {
        var first = new CanvasViewModel();
        var second = new CanvasViewModel();
        CanvasViewModel current = first;

        var provider = new ActiveCanvasProvider(() => current);

        Assert.Same(first, provider.GetActive());

        current = second;
        Assert.Same(second, provider.GetActive());
    }

    [Fact]
    public void ActiveCanvasProvider_ImplementsIActiveCanvasProvider()
    {
        Assert.True(typeof(IActiveCanvasProvider).IsAssignableFrom(typeof(ActiveCanvasProvider)));
    }

    [Fact]
    public void ActiveCanvasProvider_IsSealed()
    {
        Assert.True(typeof(ActiveCanvasProvider).IsSealed);
    }
}

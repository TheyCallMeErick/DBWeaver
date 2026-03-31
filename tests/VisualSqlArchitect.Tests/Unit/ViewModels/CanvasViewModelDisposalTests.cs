using System.ComponentModel;
using System.Collections.Specialized;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

/// <summary>
/// Tests for CanvasViewModel disposal and memory leak prevention.
/// Regression tests for: ViewModel not disposed when creating new canvas (Ctrl+N)
/// </summary>
public class CanvasViewModelDisposalTests
{
    [Fact]
    public void CanvasViewModel_Implements_IDisposable()
    {
        // Verify CanvasViewModel implements IDisposable interface
        var canvas = new CanvasViewModel();
        Assert.IsAssignableFrom<IDisposable>(canvas);
    }

    [Fact]
    public void CanvasViewModel_Has_EventHandlerFields()
    {
        // Verify that event handlers are stored in fields for proper disposal
        var type = typeof(CanvasViewModel);

        var liveSqlField = type.GetField("_liveSqlPropertyChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(liveSqlField);

        var selfField = type.GetField("_selfPropertyChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(selfField);

        var nodesField = type.GetField("_nodesCollectionChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(nodesField);

        var connectionsField = type.GetField("_connectionsCollectionChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(connectionsField);
    }

    [Fact]
    public void CanvasViewModel_Dispose_DoesNotThrow()
    {
        // Verify that disposing CanvasViewModel doesn't throw any exceptions
        var canvas = new CanvasViewModel();

        // Should not throw
        canvas.Dispose();

        // Can dispose multiple times without errors
        canvas.Dispose();
    }

    [Fact]
    public void CanvasViewModel_Disposal_Clears_NodeValidationHandlers()
    {
        // Verify that node validation handlers dictionary is cleared
        var canvas = new CanvasViewModel();

        // Get the handlers dictionary
        var handlersDictField = typeof(CanvasViewModel).GetField("_nodeValidationHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var handlersDict = (Dictionary<NodeViewModel, PropertyChangedEventHandler>)handlersDictField?.GetValue(canvas)!;

        // Dispose and verify handlers are cleared
        canvas.Dispose();
        Assert.Empty(handlersDict);
    }

    [Fact]
    public void CanvasViewModel_CollectionChanged_Handlers_Stored_In_Fields()
    {
        // Verify that collection changed handlers are stored and can be accessed
        var canvas = new CanvasViewModel();
        var type = typeof(CanvasViewModel);

        // Get the handler fields
        var nodesField = type.GetField("_nodesCollectionChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var connectionsField = type.GetField("_connectionsCollectionChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Handlers should be non-null after initialization
        var nodesHandlerValue = nodesField?.GetValue(canvas);
        var connectionsHandlerValue = connectionsField?.GetValue(canvas);

        Assert.NotNull(nodesHandlerValue);
        Assert.NotNull(connectionsHandlerValue);
    }

    [Fact]
    public void RegressionTest_MemoryLeak_OldViewModelDisposed()
    {
        // Regression test for: Old ViewModel not disposed when creating new canvas
        // This simulates the Ctrl+N scenario to ensure old ViewModel is cleaned up

        var oldCanvas = new CanvasViewModel();

        // Store node count before marking as dirty
        int nodeCountBefore = oldCanvas.Nodes.Count;

        // Mark old canvas as dirty (simulating some interaction)
        oldCanvas.IsDirty = true;

        // Simulate creating new canvas (Ctrl+N behavior)
        // In MainWindow.CreateNewCanvas, it should call oldCanvas.Dispose()
        oldCanvas.Dispose();

        // After dispose, verify handlers are cleaned up
        var handlersDictField = typeof(CanvasViewModel).GetField("_nodeValidationHandlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handlersDict = (Dictionary<NodeViewModel, PropertyChangedEventHandler>)handlersDictField?.GetValue(oldCanvas)!;

        // Handlers dictionary should be cleared
        Assert.Empty(handlersDict);

        // Create new canvas
        var newCanvas = new CanvasViewModel();

        // Verify new canvas has same initial state as old one had (independent ViewModel)
        Assert.False(newCanvas.IsDirty);

        var canvas = new CanvasViewModel();
        var type = typeof(CanvasViewModel);

        // Get all handler fields
        var liveField = type.GetField("_liveSqlPropertyChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var selfField = type.GetField("_selfPropertyChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var nodesField = type.GetField("_nodesCollectionChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var connField = type.GetField("_connectionsCollectionChangedHandler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Verify all handlers are stored (not anonymous lambdas)
        var live = liveField?.GetValue(canvas);
        var self = selfField?.GetValue(canvas);
        var nodes = nodesField?.GetValue(canvas);
        var conn = connField?.GetValue(canvas);

        // Handlers should not be null - they're now stored fields
        Assert.NotNull(live);
        Assert.NotNull(self);
        Assert.NotNull(nodes);
        Assert.NotNull(conn);
    }

    [Fact]
    public void RegressionTest_Dispose_IsIdempotent()
    {
        // Regression test for: Ensure Dispose() is idempotent (can be called multiple times)
        var canvas = new CanvasViewModel();

        // Multiple disposes should not throw
        canvas.Dispose();
        canvas.Dispose();
        canvas.Dispose();

        Assert.True(true);
    }
}

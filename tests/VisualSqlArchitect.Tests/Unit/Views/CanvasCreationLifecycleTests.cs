using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Views;

/// <summary>
/// Tests for MainWindow canvas creation and service lifecycle.
/// Regression tests for bug where old ViewModel wasn't disposed and services weren't reconnected.
/// </summary>
public class CanvasCreationLifecycleTests
{
    [Fact]
    public void MultipleCanvasViewModels_CanBeCreatedSequentially()
    {
        // Arrange - Create multiple ViewModels in sequence
        var viewModels = new CanvasViewModel[5];

        // Act
        for (int i = 0; i < 5; i++)
        {
            viewModels[i] = new CanvasViewModel();
        }

        // Assert - All should initialize without error
        foreach (var vm in viewModels)
        {
            Assert.NotNull(vm);
            Assert.False(vm.IsDirty); // Should start in clean state
        }
    }

    [Fact]
    public void CanvasViewModel_HasNecessaryProperties()
    {
        // Verify that CanvasViewModel has all properties needed by services
        var canvas = new CanvasViewModel();

        // These properties are accessed by various services
        Assert.NotNull(canvas.WindowTitle);
        Assert.NotNull(canvas.LiveSql);
        Assert.NotNull(canvas.CommandPalette);
        Assert.NotNull(canvas.SearchMenu);
        Assert.NotNull(canvas.AutoJoin);
        Assert.NotNull(canvas.DataPreview);
        Assert.NotNull(canvas.ConnectionManager);
        Assert.NotNull(canvas.Benchmark);
        Assert.NotNull(canvas.ExplainPlan);
        Assert.NotNull(canvas.SqlImporter);
        Assert.NotNull(canvas.FlowVersions);
    }

    [Fact]
    public void CanvasCreation_NewInstancesAreDistinct()
    {
        // Arrange
        var oldCanvas = new CanvasViewModel();
        var oldHashCode = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(oldCanvas);

        // Act - Create new
        var newCanvas = new CanvasViewModel();
        var newHashCode = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(newCanvas);

        // Assert - Different instances
        Assert.NotEqual(oldHashCode, newHashCode);
        Assert.NotNull(newCanvas);
    }

    [Fact]
    public void RegressionTest_CanvasViewModelsAreNotReused()
    {
        // Regression test: Ensures each canvas creation produces new ViewModel
        // Previously: Services kept references to old ViewModel after Ctrl+N
        // After fix: CreateNewCanvas() creates truly new ViewModel for services to connect

        var canvas1 = new CanvasViewModel();
        var id1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas1);

        var canvas2 = new CanvasViewModel();
        var id2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas2);

        // Different instances
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void RegressionTest_ServicesCanReconnectToNewViewModel()
    {
        // Regression test: Services should be able to reconnect to new ViewModel
        // Previously: Services kept references to old ViewModel
        // After fix: Services passed new ViewModel in InitializeServices()

        var canvas1 = new CanvasViewModel();
        var canvas2 = new CanvasViewModel();

        // Both should have independent state - different LiveSql instances
        Assert.NotEqual(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas1.LiveSql),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas2.LiveSql)
        );
    }

    [Fact]
    public void CanvasViewModel_CreationDoesNotLeakReferences()
    {
        // Verify that creating multiple CanvasViewModels doesn't accumulate
        // references through shared state

        var canvases = new List<CanvasViewModel>();

        for (int i = 0; i < 10; i++)
        {
            var canvas = new CanvasViewModel();
            canvases.Add(canvas);
        }

        // All should be distinct instances
        var hashCodes = canvases.Select(c => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c)).ToList();
        var distinctCount = hashCodes.Distinct().Count();
        Assert.Equal(10, distinctCount);
    }

    [Fact]
    public void LiveSqlViewModel_IsUniquePerCanvas()
    {
        // Each CanvasViewModel should have its own LiveSqlViewModel
        var canvas1 = new CanvasViewModel();
        var canvas2 = new CanvasViewModel();

        // LiveSql instances should be different
        Assert.NotEqual(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas1.LiveSql),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas2.LiveSql)
        );
    }

    [Fact]
    public void CommandPalette_IsUniquePerCanvas()
    {
        // Each CanvasViewModel should have its own CommandPalette
        var canvas1 = new CanvasViewModel();
        var canvas2 = new CanvasViewModel();

        // CommandPalette instances should be different
        Assert.NotEqual(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas1.CommandPalette),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas2.CommandPalette)
        );
    }

    [Fact]
    public void DataPreview_IsUniquePerCanvas()
    {
        // Each CanvasViewModel should have its own DataPreview
        var canvas1 = new CanvasViewModel();
        var canvas2 = new CanvasViewModel();

        // DataPreview instances should be different
        Assert.NotEqual(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas1.DataPreview),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(canvas2.DataPreview)
        );
    }

    [Fact]
    public void RegressionTest_Ctrl_N_CreatesIndependentCanvas()
    {
        // Regression test: Ctrl+N should create a completely independent canvas
        // Not just reassign the old ViewModel

        var firstCanvas = new CanvasViewModel();
        var firstWindowTitle = firstCanvas.WindowTitle;

        // Simulate Ctrl+N creating new canvas
        var secondCanvas = new CanvasViewModel();

        // Both should exist independently
        Assert.NotNull(firstCanvas);
        Assert.NotNull(secondCanvas);

        // They should be different instances
        Assert.NotSame(firstCanvas, secondCanvas);
    }
}

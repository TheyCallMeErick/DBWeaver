using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SqlImporterViewModelInputLimitTests
{
    [Fact]
    public async Task ImportAsync_WhenInputExceedsConfiguredLimit_DoesNotMutateCanvasAndShowsFriendlyMessage()
    {
        var canvas = new CanvasViewModel();
        int nodesBefore = canvas.Nodes.Count;
        int connectionsBefore = canvas.Connections.Count;

        canvas.SqlImporter.MaxSqlInputLength = 10;
        canvas.SqlImporter.SqlInput = "SELECT id, name FROM public.customers";

        await canvas.SqlImporter.ImportAsync();

        Assert.False(canvas.SqlImporter.IsImporting);
        Assert.False(canvas.SqlImporter.HasReport);
        Assert.Contains("too large", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("split the query", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("increase the import limit", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nodesBefore, canvas.Nodes.Count);
        Assert.Equal(connectionsBefore, canvas.Connections.Count);
    }

    [Fact]
    public async Task ImportAsync_WhenTimeoutExceeded_DoesNotMutateCanvasAndShowsTimeoutMessage()
    {
        var canvas = new CanvasViewModel();
        int nodesBefore = canvas.Nodes.Count;
        int connectionsBefore = canvas.Connections.Count;

        canvas.SqlImporter.ImportTimeout = TimeSpan.FromMilliseconds(5);
        canvas.SqlImporter.ImportStartDelayMs = 80;
        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.False(canvas.SqlImporter.IsImporting);
        Assert.False(canvas.SqlImporter.HasReport);
        Assert.Contains("timed out", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("try a smaller query", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("increase timeout", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nodesBefore, canvas.Nodes.Count);
        Assert.Equal(connectionsBefore, canvas.Connections.Count);
    }

    [Fact]
    public async Task ImportAsync_WhenCancelledByUser_DoesNotMutateCanvasAndShowsCancelledMessage()
    {
        var canvas = new CanvasViewModel();
        int nodesBefore = canvas.Nodes.Count;
        int connectionsBefore = canvas.Connections.Count;

        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(10);
        canvas.SqlImporter.ImportStartDelayMs = 250;
        canvas.SqlImporter.SqlInput = "SELECT id, name FROM public.customers";

        Task importTask = canvas.SqlImporter.ImportAsync();
        await Task.Delay(20);
        canvas.SqlImporter.CancelImport();
        await importTask;

        Assert.False(canvas.SqlImporter.IsImporting);
        Assert.False(canvas.SqlImporter.HasReport);
        Assert.Contains("cancelled", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nodesBefore, canvas.Nodes.Count);
        Assert.Equal(connectionsBefore, canvas.Connections.Count);
    }

    [Fact]
    public async Task ImportAsync_WhenCancelledByUser_CanImportAgainWithoutLockingUi()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(10);
        canvas.SqlImporter.ImportStartDelayMs = 250;
        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders";

        Task firstImport = canvas.SqlImporter.ImportAsync();
        await Task.Delay(20);
        canvas.SqlImporter.CancelImport();
        await firstImport;

        Assert.False(canvas.SqlImporter.IsImporting);

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.SqlInput = "SELECT * FROM public.orders";
        await canvas.SqlImporter.ImportAsync();

        Assert.False(canvas.SqlImporter.IsImporting);
        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}

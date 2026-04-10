using Avalonia;
using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

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
        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
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
        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
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
        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
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
        Assert.True(canvas.SqlImporter.HasReport);
    }

    [Fact]
    public async Task ImportAsync_WhenCanvasNotEmpty_RequestsConfirmationAndDoesNotMutateUntilConfirmed()
    {
        var canvas = new CanvasViewModel();
        var existingNode = new NodeViewModel("manual.node", [], new Point(0, 0));
        canvas.Nodes.Add(existingNode);

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.True(canvas.SqlImporter.IsClearCanvasConfirmationVisible);
        Assert.False(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.Nodes, n => n.Id == existingNode.Id);
    }

    [Fact]
    public async Task CancelClearCanvasConfirmation_KeepsCanvasUnchanged()
    {
        var canvas = new CanvasViewModel();
        var existingNode = new NodeViewModel("manual.node", [], new Point(0, 0));
        canvas.Nodes.Add(existingNode);

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders";

        await canvas.SqlImporter.ImportAsync();
        canvas.SqlImporter.CancelClearCanvasConfirmation();

        Assert.False(canvas.SqlImporter.IsClearCanvasConfirmationVisible);
        Assert.Contains(canvas.Nodes, n => n.Id == existingNode.Id);
        Assert.False(canvas.SqlImporter.HasReport);
    }

    [Fact]
    public async Task ConfirmClearCanvasAndImportAsync_WhenRequested_ReplacesCanvasAndImports()
    {
        var canvas = new CanvasViewModel();
        var existingNode = new NodeViewModel("manual.node", [], new Point(0, 0));
        canvas.Nodes.Add(existingNode);

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders";

        await canvas.SqlImporter.ImportAsync();
        await canvas.SqlImporter.ConfirmClearCanvasAndImportAsync();

        Assert.False(canvas.SqlImporter.IsClearCanvasConfirmationVisible);
        Assert.True(canvas.SqlImporter.HasReport);
        Assert.DoesNotContain(canvas.Nodes, n => n.Id == existingNode.Id);
        Assert.True(canvas.Nodes.Count > 0);
    }
}



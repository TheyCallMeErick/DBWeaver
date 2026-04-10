using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlImporterFocusReportItemTests
{
    [Fact]
    public async Task FocusReportItem_WhenLinkedItemExists_SelectsCorrespondingNode()
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.SqlInput = "SELECT * FROM public.orders";

        await canvas.SqlImporter.ImportAsync();

        var linked = canvas.SqlImporter.Report.FirstOrDefault(r => r.CanFocusNode);
        Assert.NotNull(linked);

        bool focused = canvas.SqlImporter.FocusReportItem(linked);

        Assert.True(focused);
        Assert.Contains(canvas.Nodes, n => n.IsSelected && n.Id == linked!.SourceNodeId);
    }

    [Fact]
    public void FocusReportItem_WhenItemHasNoNodeLink_ReturnsFalse()
    {
        var canvas = new CanvasViewModel();
        var item = new ImportReportItem("No link", ImportItemStatus.Skipped, "reason");

        bool focused = canvas.SqlImporter.FocusReportItem(item);

        Assert.False(focused);
    }
}



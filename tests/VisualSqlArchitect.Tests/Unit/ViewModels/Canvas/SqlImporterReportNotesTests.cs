using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SqlImporterReportNotesTests
{
    [Fact]
    public void ImportReportItem_PartialOrSkippedWithoutNote_AddsDefaultJustification()
    {
        var partial = new ImportReportItem("partial", ImportItemStatus.Partial);
        var skipped = new ImportReportItem("skipped", ImportItemStatus.Skipped, " ");
        var imported = new ImportReportItem("imported", ImportItemStatus.Imported);

        Assert.False(string.IsNullOrWhiteSpace(partial.Note));
        Assert.False(string.IsNullOrWhiteSpace(skipped.Note));
        Assert.True(string.IsNullOrWhiteSpace(imported.Note));
    }

    [Fact]
    public async Task ImportAsync_PartialAndSkippedItems_AlwaysHaveClearNotes()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT unknown_col FROM public.orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);

        var problematicItems = canvas.SqlImporter.Report
            .Where(i => i.IsPartial || i.IsSkipped)
            .ToList();

        Assert.NotEmpty(problematicItems);
        Assert.All(problematicItems, i => Assert.False(string.IsNullOrWhiteSpace(i.Note)));
    }

    [Fact]
    public async Task ImportAsync_GroupByWithUngroupedColumn_ReportsGroupingConflict()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id, customer_id FROM orders GROUP BY customer_id";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Partial
            && item.Label.Contains("GROUP BY conflict", StringComparison.OrdinalIgnoreCase)
            && (item.Note ?? string.Empty).Contains("neither grouped nor aggregated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_GroupByWithAggregateProjection_DoesNotReportGroupingConflict()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT customer_id, COUNT(*) AS total FROM orders GROUP BY customer_id";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(canvas.SqlImporter.Report, item =>
            item.Label.Contains("GROUP BY conflict", StringComparison.OrdinalIgnoreCase));
    }
}

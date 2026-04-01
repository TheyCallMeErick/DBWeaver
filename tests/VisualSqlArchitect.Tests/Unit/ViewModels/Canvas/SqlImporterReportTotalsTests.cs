using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SqlImporterReportTotalsTests
{
    [Fact]
    public async Task ImportAsync_WhenSuccessful_PopulatesReportTotals()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT * FROM public.orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.ReportImportedCount > 0);
        Assert.True(canvas.SqlImporter.ReportPartialCount >= 0);
        Assert.True(canvas.SqlImporter.ReportSkippedCount >= 0);
    }

    [Fact]
    public async Task ImportAsync_WhenRejectedByInputLimit_ResetsReportTotals()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.MaxSqlInputLength = 5;
        canvas.SqlImporter.SqlInput = "SELECT * FROM public.orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.Contains("too large", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, canvas.SqlImporter.ReportImportedCount);
        Assert.Equal(0, canvas.SqlImporter.ReportPartialCount);
        Assert.Equal(0, canvas.SqlImporter.ReportSkippedCount);
    }
}

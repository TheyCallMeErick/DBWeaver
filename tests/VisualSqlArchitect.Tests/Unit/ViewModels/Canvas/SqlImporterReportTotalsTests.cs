using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

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

        Assert.True(canvas.SqlImporter.HasReport);
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

        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
        Assert.Equal(0, canvas.SqlImporter.ReportImportedCount);
        Assert.Equal(0, canvas.SqlImporter.ReportPartialCount);
        Assert.Equal(0, canvas.SqlImporter.ReportSkippedCount);
    }
}



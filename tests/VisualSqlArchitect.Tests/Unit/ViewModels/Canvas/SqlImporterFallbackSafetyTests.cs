using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlImporterFallbackSafetyTests
{
    [Fact]
    public async Task ImportAsync_WithUnsupportedSubquery_BlocksRawFallbackWithSafetyMessage()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id FROM (SELECT id FROM public.orders) o";

        await canvas.SqlImporter.ImportAsync();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Skipped
            && item.Label.Contains("Raw fallback", StringComparison.OrdinalIgnoreCase)
            && (item.Note ?? string.Empty).Contains("disabled", StringComparison.OrdinalIgnoreCase)
            && (item.Note ?? string.Empty).Contains("unsafe", StringComparison.OrdinalIgnoreCase));
    }
}



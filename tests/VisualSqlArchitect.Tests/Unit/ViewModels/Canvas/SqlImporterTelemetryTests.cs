using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlImporterTelemetryTests
{
    [Fact]
    public async Task ImportAsync_WhenSuccessful_TracksParseMapBuildTelemetry()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT * FROM orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.True(canvas.SqlImporter.HasReport);

        Assert.True(canvas.SqlImporter.LastTotalDurationMs > 0);
        Assert.True(canvas.SqlImporter.LastParseDurationMs >= 0);
        Assert.True(canvas.SqlImporter.LastMapDurationMs >= 0);
        Assert.True(canvas.SqlImporter.LastBuildDurationMs >= 0);

        Assert.True(canvas.SqlImporter.LastTotalDurationMs >= canvas.SqlImporter.LastParseDurationMs);
        Assert.True(canvas.SqlImporter.LastTotalDurationMs >= canvas.SqlImporter.LastMapDurationMs);
        Assert.True(canvas.SqlImporter.LastTotalDurationMs >= canvas.SqlImporter.LastBuildDurationMs);
    }

    [Fact]
    public async Task ImportAsync_WhenRejectedByInputLimit_LeavesTelemetryZeroed()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.MaxSqlInputLength = 5;
        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders";

        await canvas.SqlImporter.ImportAsync();

        Assert.False(canvas.SqlImporter.HasReport);
        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
        Assert.Equal(0, canvas.SqlImporter.LastParseDurationMs);
        Assert.Equal(0, canvas.SqlImporter.LastMapDurationMs);
        Assert.Equal(0, canvas.SqlImporter.LastBuildDurationMs);
        Assert.Equal(0, canvas.SqlImporter.LastTotalDurationMs);
    }
}



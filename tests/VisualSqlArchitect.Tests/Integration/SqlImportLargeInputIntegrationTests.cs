
using Xunit;

namespace Integration;

public class SqlImportLargeInputIntegrationTests
{
    [Fact]
    public async Task ImportAsync_WithLargeSqlInput_CompletesWithoutBreakingFlow()
    {
        var canvas = new CanvasViewModel();

        // Build a long but valid SQL statement to exercise large-input import flow.
        string projection = string.Join(", ", Enumerable.Repeat("id", 1200));
        string sql = $"SELECT {projection} FROM public.orders";

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(20);
        canvas.SqlImporter.MaxSqlInputLength = 100_000;
        canvas.SqlImporter.SqlInput = sql;

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.True(canvas.SqlImporter.ReportImportedCount > 0);
        Assert.True(canvas.SqlImporter.LastTotalDurationMs > 0);
    }
}

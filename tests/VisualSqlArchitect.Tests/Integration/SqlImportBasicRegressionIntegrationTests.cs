using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using Xunit;

namespace VisualSqlArchitect.Tests.Integration;

public class SqlImportBasicRegressionIntegrationTests
{
    [Fact]
    public async Task BasicImport_SelectWithSimpleWhere_RemainsStable()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders WHERE id = 10";

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("select", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BasicImport_SelectWithAlias_RemainsStable()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id AS order_id FROM public.orders";

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Label.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("select", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BasicImport_SelectMultipleColumns_RemainsStable()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id, customer_id FROM public.orders";

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("select", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BasicImport_JoinWhere_ReportListsImportedItemsClearly()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT orders.id FROM orders INNER JOIN customers ON orders.customer_id = customers.id WHERE orders.id = 10";

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.HasReport);
        Assert.True(canvas.SqlImporter.ReportImportedCount >= 4);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("FROM:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BasicImport_SupportedQuery_RecompilesPreviewWithoutErrors()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT orders.id FROM orders INNER JOIN customers ON orders.customer_id = customers.id WHERE orders.id = 10";

        await canvas.SqlImporter.ImportAsync();
        canvas.LiveSql.Recompile();

        Assert.Contains("Done", canvas.SqlImporter.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.SqlImporter.HasReport);
        Assert.True(canvas.LiveSql.IsValid, string.Join(" | ", canvas.LiveSql.ErrorHints));
        Assert.Empty(canvas.LiveSql.ErrorHints);
        Assert.DoesNotContain("-- Add a", canvas.LiveSql.RawSql, StringComparison.OrdinalIgnoreCase);
    }
}

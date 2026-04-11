
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

public class SqlImportPartialFallbackIntegrationTests
{
    [Fact]
    public async Task ImportAsync_WithValidSimpleCte_ImportsWithoutFallback()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "WITH cte AS (SELECT id FROM public.orders) SELECT id FROM cte";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("CTE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Skipped
            && r.Label.Contains("CTE / sub-query", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithPartiallySupportedClauses_CompletesAndProvidesClearDiagnostics()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id FROM public.orders UNION SELECT id FROM public.customers";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);

        var problematic = canvas.SqlImporter.Report
            .Where(r => r.Status is ImportItemStatus.Partial or ImportItemStatus.Skipped)
            .ToList();

        Assert.NotEmpty(problematic);
        Assert.All(problematic, item => Assert.False(string.IsNullOrWhiteSpace(item.Note)));
        Assert.Contains(problematic, item => item.Label.Contains("UNION", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithCorrelatedSubquery_ImportsExistsCondition()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT o.id FROM public.orders o WHERE EXISTS (SELECT 1 FROM public.order_items i WHERE i.order_id = o.id)";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Imported
            && item.Label.Contains("EXISTS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithCorrelatedSubquery_ReportsExternalCorrelationFields()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT o.id FROM public.orders o WHERE EXISTS (SELECT 1 FROM public.order_items i WHERE i.order_id = o.id AND i.customer_id = o.customer_id)";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Imported
            && item.Label.Contains("WHERE EXISTS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithInvalidCteName_ReportsDiagnostic()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "WITH 1cte AS (SELECT id FROM public.orders) SELECT id FROM 1cte";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Partial
            && item.Label.Contains("CTE name diagnostics", StringComparison.OrdinalIgnoreCase)
            && (item.Note ?? string.Empty).Contains("Invalid CTE name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithDuplicateCteName_ReportsDiagnostic()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "WITH cte AS (SELECT id FROM public.orders), cte AS (SELECT id FROM public.customers) SELECT id FROM cte";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Partial
            && item.Label.Contains("CTE name diagnostics", StringComparison.OrdinalIgnoreCase)
            && (item.Note ?? string.Empty).Contains("Duplicate CTE name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_WithSimpleCte_ImportsSuccessfully()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "WITH cte_orders AS (SELECT id FROM public.orders) SELECT id FROM cte_orders";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Imported
            && item.Label.Contains("CTE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(canvas.SqlImporter.Report, item =>
            item.Label.Contains("CTE / sub-query", StringComparison.OrdinalIgnoreCase)
            && item.Status == ImportItemStatus.Skipped);
    }

    [Fact]
    public async Task ImportAsync_WithChainedSimpleCtes_ImportsSuccessfully()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "WITH cte_a AS (SELECT id FROM public.orders), cte_b AS (SELECT id FROM cte_a) SELECT id FROM cte_b";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, item =>
            item.Status == ImportItemStatus.Imported
            && item.Label.Contains("CTE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(canvas.SqlImporter.Report, item =>
            item.Label.Contains("CTE / sub-query", StringComparison.OrdinalIgnoreCase)
            && item.Status == ImportItemStatus.Skipped);
    }
}

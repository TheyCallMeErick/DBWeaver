
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

public class SqlImportRoundTripTests
{
    [Fact]
    public async Task RoundTrip_BasicSelectAndLimit_KeepsLegacyImportFlowStable()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders LIMIT 3";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();

        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Label.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
            && r.Status == ImportItemStatus.Imported);
        Assert.Contains("select", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(sql));
    }

    [Fact]
    public async Task RoundTrip_BasicSelectStar_RemainsOperational()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT * FROM public.orders";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();

        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains("select", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(sql));
    }

    [Fact]
    public async Task RoundTrip_JoinScenario_ReportsJoinImport()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT * FROM public.orders INNER JOIN public.customers ON orders.customer_id = customers.id";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RoundTrip_OrderByScenario_PreservesDirectionInGeneratedSql()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders ORDER BY id DESC";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoundTrip_OrderByMultipleTerms_PreservesTermOrderInGeneratedSql()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id, customer_id FROM orders ORDER BY customer_id ASC, id DESC";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;
        string sqlUpper = sql.ToUpperInvariant();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("ORDER BY", sqlUpper, StringComparison.Ordinal);

        int customerIdx = sqlUpper.IndexOf("CUSTOMER_ID", StringComparison.Ordinal);
        int idIdx = sqlUpper.LastIndexOf("ID", StringComparison.Ordinal);
        Assert.True(customerIdx >= 0, "Expected CUSTOMER_ID in generated ORDER BY.");
        Assert.True(idIdx >= 0, "Expected ID term in generated ORDER BY.");
        Assert.True(customerIdx < idIdx, "Expected CUSTOMER_ID term to come before ID term.");
    }

    [Fact]
    public async Task RoundTrip_OrderByAlias_ImportsAndCompilesOrdering()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT customer_id AS cid FROM orders ORDER BY cid DESC";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoundTrip_GroupByMultipleTerms_PreservesGroupingOrderInGeneratedSql()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT customer_id, id FROM orders GROUP BY customer_id, id";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;
        string sqlUpper = sql.ToUpperInvariant();

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("GROUP BY", sqlUpper, StringComparison.Ordinal);

        int customerIdx = sqlUpper.IndexOf("CUSTOMER_ID", StringComparison.Ordinal);
        int idIdx = sqlUpper.LastIndexOf("ID", StringComparison.Ordinal);
        Assert.True(customerIdx >= 0, "Expected CUSTOMER_ID in generated GROUP BY.");
        Assert.True(idIdx >= 0, "Expected ID term in generated GROUP BY.");
        Assert.True(customerIdx < idIdx, "Expected CUSTOMER_ID term to come before ID term.");
    }

    [Fact]
    public async Task RoundTrip_GroupByHavingCountStar_EmitsHavingPredicate()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT customer_id FROM orders GROUP BY customer_id HAVING COUNT(*) > 1";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("HAVING COUNT(*)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HAVING", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoundTrip_WhereExistsSubquery_ImportsAndEmitsExistsClause()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id FROM orders WHERE EXISTS (SELECT 1 FROM order_items WHERE order_items.order_id = orders.id)";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("EXISTS", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoundTrip_WhereInSubquery_ImportsAndEmitsInClause()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id FROM orders WHERE id IN (SELECT order_id FROM order_items)";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains(" IN(", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(" IN ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoundTrip_WhereScalarSubquery_ImportsAndEmitsScalarPredicate()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput =
            "SELECT id FROM orders WHERE id > (SELECT MAX(id) FROM orders)";

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        canvas.LiveSql.Recompile();
        string sql = canvas.LiveSql.RawSql;

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.Contains(canvas.SqlImporter.Report, r =>
            r.Status == ImportItemStatus.Imported
            && r.Label.Contains("scalar sub-query", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("SELECT MAX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">", sql, StringComparison.OrdinalIgnoreCase);
    }
}

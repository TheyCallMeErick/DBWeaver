using System.Text.RegularExpressions;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

public class SqlImportComprehensiveScenarioMatrixIntegrationTests
{
    [Fact]
    public void ComprehensiveMatrix_DefinesAtLeastNinetyScenarios()
    {
        Assert.True(Scenarios.Count >= 90, $"Expected at least 90 scenarios, found {Scenarios.Count}.");
    }

    [Theory]
    [MemberData(nameof(GetScenarioData))]
    public async Task ImportScenario_CompletesAndValidatesExpectedBehavior(ComprehensiveScenario scenario)
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);

        await ImportSqlWithAutoConfirmationAsync(canvas, scenario.Sql);

        if (scenario.RequireReport)
            Assert.True(canvas.SqlImporter.HasReport);

        if (!canvas.SqlImporter.HasReport && scenario.AllowNoReport)
            return;

        if (scenario.ExpectStrictRoundTrip)
            Assert.True(canvas.Nodes.Count(n => n.IsTableSource) >= scenario.MinimumSources);

        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        AssertResultPins(canvas, scenario.ExpectedResultInputPins);

        if (scenario.ExpectPartialOrSkipped)
        {
            Assert.Contains(canvas.SqlImporter.Report, item =>
                item.Status is ImportItemStatus.Partial or ImportItemStatus.Skipped);
        }

        if (!scenario.ExpectStrictRoundTrip)
            return;

        canvas.LiveSql.Recompile();
        string firstGeneratedSql = canvas.LiveSql.RawSql;
        Assert.False(string.IsNullOrWhiteSpace(firstGeneratedSql));
        Assert.DoesNotContain("-- Add a table", firstGeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-- Connect columns", firstGeneratedSql, StringComparison.OrdinalIgnoreCase);

        await ImportSqlWithAutoConfirmationAsync(canvas, firstGeneratedSql);
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
        AssertResultPins(canvas, scenario.ExpectedResultInputPins);

        canvas.LiveSql.Recompile();
        string secondGeneratedSql = canvas.LiveSql.RawSql;
        Assert.Equal(CanonicalizeSql(firstGeneratedSql), CanonicalizeSql(secondGeneratedSql));
    }

    [Fact]
    public async Task ImportScenario_WhenCanvasNotEmpty_ConfirmClearBehaviorWorksForConfirmAndCancel()
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);

        NodeViewModel seed = new(NodeDefinitionRegistry.Get(NodeType.ResultOutput), default);
        canvas.Nodes.Add(seed);

        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders LIMIT 5";
        await canvas.SqlImporter.ImportAsync();

        Assert.True(canvas.SqlImporter.IsClearCanvasConfirmationVisible);
        canvas.SqlImporter.CancelClearCanvasConfirmation();
        Assert.False(canvas.SqlImporter.IsClearCanvasConfirmationVisible);
        Assert.Contains(canvas.Nodes, n => n.Id == seed.Id);

        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders LIMIT 5";
        await canvas.SqlImporter.ImportAsync();
        Assert.True(canvas.SqlImporter.IsClearCanvasConfirmationVisible);

        await canvas.SqlImporter.ConfirmClearCanvasAndImportAsync();
        Assert.False(canvas.SqlImporter.IsClearCanvasConfirmationVisible);
        Assert.DoesNotContain(canvas.Nodes, n => n.Id == seed.Id);
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
    }

    [Fact]
    public async Task ImportScenario_SequentialRapidImports_MaintainsConsistentState()
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);

        string[] sqls =
        [
            "SELECT id FROM public.orders LIMIT 3",
            "SELECT id, customer_id FROM public.orders WHERE orders.id >= 20 ORDER BY orders.id DESC LIMIT 10",
            "SELECT orders.id FROM public.orders JOIN public.customers ON orders.customer_id = customers.id LIMIT 7",
            "SELECT id FROM public.orders WHERE EXISTS (SELECT 1 FROM public.order_items WHERE order_items.order_id = orders.id) LIMIT 4",
        ];

        foreach (string sql in sqls)
            await ImportSqlWithAutoConfirmationAsync(canvas, sql);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.NotEmpty(canvas.Nodes);
        Assert.NotEmpty(canvas.Connections);
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);
    }

    [Fact]
    public async Task ImportScenario_InvalidSql_DoesNotCorruptExistingCanvas()
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);

        canvas.SqlImporter.SqlInput = "SELECT id FROM public.orders LIMIT 9";
        await canvas.SqlImporter.ImportAsync();

        int baselineNodes = canvas.Nodes.Count;
        int baselineConnections = canvas.Connections.Count;

        canvas.SqlImporter.SqlInput = "SELECT FROM WHERE";
        await canvas.SqlImporter.ImportAsync();

        Assert.Equal(baselineNodes, canvas.Nodes.Count);
        Assert.Equal(baselineConnections, canvas.Connections.Count);
    }

    public static IEnumerable<object[]> GetScenarioData() =>
        Scenarios.Select(s => new object[] { s });

    private static readonly IReadOnlyList<ComprehensiveScenario> Scenarios = BuildScenarios();

    private static IReadOnlyList<ComprehensiveScenario> BuildScenarios()
    {
        var scenarios = new List<ComprehensiveScenario>(capacity: 96);

        for (int i = 0; i < 12; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"order_mixed_alias_{i}",
                $"SELECT id AS oid, customer_id AS cid FROM public.orders ORDER BY cid ASC, oid DESC LIMIT {20 + i}",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["order_by", "order_by_desc", "top"]));
        }

        for (int i = 0; i < 12; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"group_alias_origin_{i}",
                $"SELECT customer_id AS cid, customer_id FROM public.orders GROUP BY cid, customer_id HAVING COUNT(*) > {1 + (i % 3)} ORDER BY customer_id ASC LIMIT {30 + i}",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["group_by", "having", "top"]));
        }

        for (int i = 0; i < 12; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"not_in_subquery_{i}",
                $"SELECT id FROM public.orders WHERE id NOT IN (SELECT order_id FROM public.order_items) ORDER BY id DESC LIMIT {10 + i}",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["where", "top"]));
        }

        for (int i = 0; i < 8; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"not_exists_subquery_{i}",
                $"SELECT id FROM public.orders WHERE NOT EXISTS (SELECT 1 FROM public.order_items WHERE order_items.order_id = orders.id) LIMIT {5 + i}",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["where", "top"]));
        }

        for (int i = 0; i < 10; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"limit_offset_{i}",
                $"SELECT id FROM public.orders ORDER BY id ASC LIMIT {15 + i} OFFSET {i}",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["order_by", "top"]));
        }

        for (int i = 0; i < 8; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"top_sqlserver_{i}",
                $"SELECT TOP {5 + i} id FROM public.orders ORDER BY id DESC",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["order_by_desc", "top"]));
        }

        for (int i = 0; i < 8; i++)
        {
            string joinType = i % 2 == 0 ? "INNER JOIN" : "LEFT JOIN";
            scenarios.Add(new ComprehensiveScenario(
                $"join_multicond_{i}",
                $"SELECT o.id FROM public.orders o {joinType} public.customers c ON o.customer_id = c.id AND o.id >= c.id LIMIT {20 + i}",
                MinimumSources: 2,
                ExpectStrictRoundTrip: false,
                ExpectedPins: ["top"]));
        }

        for (int i = 0; i < 8; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"scalar_subquery_parentheses_{i}",
                $"SELECT id FROM public.orders WHERE (orders.id >= ((SELECT MAX(id) FROM public.orders))) LIMIT {12 + i}",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["where", "top"]));
        }

        for (int i = 0; i < 8; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"quoted_identifiers_{i}",
                $"SELECT \"Order Items\".\"Id\" FROM \"Sales\".\"Order Items\" ORDER BY \"Order Items\".\"Id\" DESC LIMIT {8 + i}",
                MinimumSources: 1,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["order_by_desc", "top"]));
        }

        for (int i = 0; i < 10; i++)
        {
            scenarios.Add(new ComprehensiveScenario(
                $"distinct_join_group_having_{i}",
                $"SELECT DISTINCT orders.customer_id FROM public.orders JOIN public.customers ON orders.customer_id = customers.id GROUP BY orders.customer_id HAVING COUNT(*) > {1 + (i % 4)} ORDER BY orders.customer_id DESC LIMIT {25 + i}",
                MinimumSources: 2,
                ExpectStrictRoundTrip: true,
                ExpectedPins: ["group_by", "having", "order_by_desc", "top"]));
        }

        Assert.Equal(96, scenarios.Count);
        return scenarios;
    }

    private static void AssertResultPins(CanvasViewModel canvas, IReadOnlyList<string> expectedInputPins)
    {
        if (expectedInputPins.Count == 0)
            return;

        NodeViewModel? result = canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.ResultOutput);
        if (result is null)
            return;

        foreach (string pinName in expectedInputPins)
        {
            Assert.Contains(canvas.Connections, c =>
                c.ToPin?.Owner == result
                && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static async Task ImportSqlWithAutoConfirmationAsync(CanvasViewModel canvas, string sql)
    {
        canvas.SqlImporter.SqlInput = sql;
        await canvas.SqlImporter.ImportAsync();

        if (canvas.SqlImporter.IsClearCanvasConfirmationVisible)
            await canvas.SqlImporter.ConfirmClearCanvasAndImportAsync();
    }

    private static string CanonicalizeSql(string sql)
    {
        string normalized = Regex.Replace(sql, @"\s+", " ").Trim();
        normalized = normalized.TrimEnd(';');
        return normalized.ToUpperInvariant();
    }

    public sealed record ComprehensiveScenario(
        string Name,
        string Sql,
        int MinimumSources,
        bool ExpectStrictRoundTrip,
        bool ExpectPartialOrSkipped = false,
        bool RequireReport = true,
        bool AllowNoReport = false,
        IReadOnlyList<string>? ExpectedPins = null)
    {
        public IReadOnlyList<string> ExpectedResultInputPins { get; } = ExpectedPins ?? [];
        public override string ToString() => Name;
    }
}

using System.Text.RegularExpressions;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

public class SqlImportNodeCoverageMatrixIntegrationTests
{
    [Fact]
    public void NodeCoverageMatrix_DefinesAtLeastEightyScenarios()
    {
        Assert.True(NodeCoverageScenarios.Count >= 80, $"Expected at least 80 scenarios, found {NodeCoverageScenarios.Count}.");
    }

    [Theory]
    [MemberData(nameof(GetNodeCoverageScenarioData))]
    public async Task RoundTrip_NodeCoverageScenarios_PreserveExpectedNodeTypes(NodeCoverageScenario scenario)
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);

        await ImportSqlWithAutoConfirmationAsync(canvas, scenario.Sql);
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.Nodes.Count(n => n.IsTableSource) >= scenario.ExpectedMinimumSources);
        foreach (NodeType expectedType in scenario.ExpectedNodeTypes)
            Assert.Contains(canvas.Nodes, n => n.Type == expectedType);

        AssertCoreProjectionWiring(canvas);
        AssertScenarioSpecificWiring(canvas, scenario);

        canvas.LiveSql.Recompile();
        string firstGeneratedSql = canvas.LiveSql.RawSql;
        Assert.False(string.IsNullOrWhiteSpace(firstGeneratedSql));
        Assert.DoesNotContain("-- Add a table", firstGeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-- Connect columns", firstGeneratedSql, StringComparison.OrdinalIgnoreCase);

        await ImportSqlWithAutoConfirmationAsync(canvas, firstGeneratedSql);
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        canvas.LiveSql.Recompile();
        string secondGeneratedSql = canvas.LiveSql.RawSql;
        Assert.False(string.IsNullOrWhiteSpace(secondGeneratedSql));

        Assert.Equal(CanonicalizeSql(firstGeneratedSql), CanonicalizeSql(secondGeneratedSql));
    }

    public static IEnumerable<object[]> GetNodeCoverageScenarioData() =>
        NodeCoverageScenarios.Select(scenario => new object[] { scenario });

    private static readonly IReadOnlyList<NodeCoverageScenario> NodeCoverageScenarios = BuildNodeCoverageScenarios();

    private static IReadOnlyList<NodeCoverageScenario> BuildNodeCoverageScenarios()
    {
        var scenarios = new List<NodeCoverageScenario>(capacity: 80);

        string[] simpleComparisonOperators = ["=", ">", ">=", "<", "<=", "<>"];
        NodeType[] simpleComparisonNodeTypes =
        [
            NodeType.Equals,
            NodeType.GreaterThan,
            NodeType.GreaterOrEqual,
            NodeType.LessThan,
            NodeType.LessOrEqual,
            NodeType.NotEquals,
        ];

        for (int i = 0; i < 24; i++)
        {
            int opIndex = i % simpleComparisonOperators.Length;
            string op = simpleComparisonOperators[opIndex];
            NodeType comparisonNodeType = simpleComparisonNodeTypes[opIndex];
            int value = 10 + i;

            scenarios.Add(new NodeCoverageScenario(
                $"where_comparison_{i}",
                $"SELECT id, customer_id FROM public.orders WHERE orders.id {op} {value} ORDER BY orders.id DESC LIMIT {100 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.WhereOutput,
                    NodeType.Top,
                    comparisonNodeType,
                ],
                ExpectsWhereComparisonFlow: true,
                ExpectsTopFlow: true));
        }

        string[] joinTypes = ["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN"];
        for (int i = 0; i < 16; i++)
        {
            string joinType = joinTypes[i % joinTypes.Length];
            int limit = 30 + i;

            scenarios.Add(new NodeCoverageScenario(
                $"join_type_{i}",
                $"SELECT orders.id, customers.id AS customer_row FROM public.orders {joinType} public.customers ON orders.customer_id = customers.id ORDER BY orders.id ASC LIMIT {limit}",
                ExpectedMinimumSources: 2,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.Join,
                    NodeType.Top,
                ],
                ExpectsJoinFlow: true,
                ExpectsTopFlow: true));
        }

        string[] havingOperators = ["=", ">", ">=", "<", "<=", "<>", "!="];
        NodeType[] havingComparisonNodeTypes =
        [
            NodeType.Equals,
            NodeType.GreaterThan,
            NodeType.GreaterOrEqual,
            NodeType.LessThan,
            NodeType.LessOrEqual,
            NodeType.NotEquals,
            NodeType.NotEquals,
        ];

        for (int i = 0; i < 16; i++)
        {
            int opIndex = i % havingOperators.Length;
            string op = havingOperators[opIndex];
            NodeType comparisonNodeType = havingComparisonNodeTypes[opIndex];
            int threshold = 1 + (i % 5);

            scenarios.Add(new NodeCoverageScenario(
                $"having_count_{i}",
                $"SELECT orders.status FROM public.orders GROUP BY orders.status HAVING COUNT(*) {op} {threshold} ORDER BY orders.status ASC LIMIT {60 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.CountStar,
                    comparisonNodeType,
                    NodeType.Top,
                ],
                ExpectsHavingCountFlow: true,
                ExpectsTopFlow: true));
        }

        for (int i = 0; i < 4; i++)
        {
            scenarios.Add(new NodeCoverageScenario(
                $"subquery_exists_{i}",
                $"SELECT id FROM public.orders WHERE EXISTS (SELECT 1 FROM public.order_items WHERE order_items.order_id = orders.id) LIMIT {10 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.SubqueryExists,
                    NodeType.Top,
                ],
                ExpectsSubqueryWhereFlow: true,
                ExpectsTopFlow: true));
        }

        for (int i = 0; i < 4; i++)
        {
            scenarios.Add(new NodeCoverageScenario(
                $"subquery_in_{i}",
                $"SELECT id FROM public.orders WHERE id IN (SELECT order_id FROM public.order_items) ORDER BY id DESC LIMIT {20 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.SubqueryIn,
                    NodeType.Top,
                ],
                ExpectsSubqueryWhereFlow: true,
                ExpectsTopFlow: true));
        }

        for (int i = 0; i < 4; i++)
        {
            scenarios.Add(new NodeCoverageScenario(
                $"subquery_scalar_{i}",
                $"SELECT id FROM public.orders WHERE id > (SELECT MAX(id) FROM public.orders) LIMIT {30 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.SubqueryScalar,
                    NodeType.Top,
                ],
                ExpectsSubqueryWhereFlow: true,
                ExpectsTopFlow: true));
        }

        for (int i = 0; i < 4; i++)
        {
            scenarios.Add(new NodeCoverageScenario(
                $"cte_simple_{i}",
                $"WITH src AS (SELECT id FROM public.orders) SELECT id FROM src ORDER BY id ASC LIMIT {40 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.Top,
                ],
                ExpectsTopFlow: true));
        }

        for (int i = 0; i < 4; i++)
        {
            scenarios.Add(new NodeCoverageScenario(
                $"cte_chained_{i}",
                $"WITH src AS (SELECT id FROM public.orders), src2 AS (SELECT id FROM src) SELECT id FROM src2 ORDER BY id ASC LIMIT {50 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.Top,
                ],
                ExpectsTopFlow: true));
        }

        for (int i = 0; i < 4; i++)
        {
            scenarios.Add(new NodeCoverageScenario(
                $"distinct_transform_{i}",
                $"SELECT DISTINCT id, COALESCE(status, 'unknown') AS normalized_status FROM public.orders WHERE orders.id >= {1 + i} ORDER BY id DESC LIMIT {70 + i}",
                ExpectedMinimumSources: 1,
                ExpectedNodeTypes:
                [
                    NodeType.ResultOutput,
                    NodeType.ColumnSetBuilder,
                    NodeType.WhereOutput,
                    NodeType.GreaterOrEqual,
                    NodeType.Top,
                ],
                ExpectsWhereComparisonFlow: true,
                ExpectsTopFlow: true));
        }

        Assert.Equal(80, scenarios.Count);
        return scenarios;
    }

    private static async Task ImportSqlWithAutoConfirmationAsync(CanvasViewModel canvas, string sql)
    {
        canvas.SqlImporter.SqlInput = sql;
        await canvas.SqlImporter.ImportAsync();

        if (canvas.SqlImporter.IsClearCanvasConfirmationVisible)
            await canvas.SqlImporter.ConfirmClearCanvasAndImportAsync();

        Assert.False(canvas.SqlImporter.IsClearCanvasConfirmationVisible);
        Assert.True(canvas.SqlImporter.HasReport);
    }

    private static string CanonicalizeSql(string sql)
    {
        string normalized = Regex.Replace(sql, @"\s+", " ").Trim();
        normalized = normalized.TrimEnd(';');
        return normalized.ToUpperInvariant();
    }

    public sealed record NodeCoverageScenario(
        string Name,
        string Sql,
        int ExpectedMinimumSources,
        IReadOnlyList<NodeType> ExpectedNodeTypes,
        bool ExpectsJoinFlow = false,
        bool ExpectsWhereComparisonFlow = false,
        bool ExpectsSubqueryWhereFlow = false,
        bool ExpectsHavingCountFlow = false,
        bool ExpectsTopFlow = false)
    {
        public override string ToString() => Name;
    }

    private static void AssertCoreProjectionWiring(CanvasViewModel canvas)
    {
        NodeViewModel result = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);
        NodeViewModel columnSet = canvas.Nodes.First(n => n.Type == NodeType.ColumnSetBuilder);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == columnSet
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertScenarioSpecificWiring(CanvasViewModel canvas, NodeCoverageScenario scenario)
    {
        if (scenario.ExpectsJoinFlow)
            AssertJoinWiring(canvas);

        if (scenario.ExpectsWhereComparisonFlow)
            AssertWhereComparisonWiring(canvas);

        if (scenario.ExpectsSubqueryWhereFlow)
            AssertSubqueryWhereWiring(canvas);

        if (scenario.ExpectsHavingCountFlow)
            AssertHavingCountWiring(canvas);

        if (scenario.ExpectsTopFlow)
            AssertTopWiring(canvas);
    }

    private static void AssertJoinWiring(CanvasViewModel canvas)
    {
        NodeViewModel join = canvas.Nodes.First(n => n.Type == NodeType.Join);

        Assert.Contains(canvas.Connections, c =>
            c.ToPin?.Owner == join
            && c.ToPin.Name.Equals("left", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(canvas.Connections, c =>
            c.ToPin?.Owner == join
            && c.ToPin.Name.Equals("right", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertWhereComparisonWiring(CanvasViewModel canvas)
    {
        NodeViewModel result = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);
        NodeViewModel where = canvas.Nodes.First(n => n.Type == NodeType.WhereOutput);
        NodeViewModel comparison = canvas.Nodes.First(n =>
            n.Type is NodeType.Equals
                or NodeType.NotEquals
                or NodeType.GreaterThan
                or NodeType.GreaterOrEqual
                or NodeType.LessThan
                or NodeType.LessOrEqual);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == comparison
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == where
            && c.ToPin.Name.Equals("condition", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == where
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertSubqueryWhereWiring(CanvasViewModel canvas)
    {
        NodeViewModel result = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);
        NodeViewModel subqueryNode = canvas.Nodes.First(n =>
            n.Type is NodeType.SubqueryExists or NodeType.SubqueryIn or NodeType.SubqueryScalar);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == subqueryNode
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertHavingCountWiring(CanvasViewModel canvas)
    {
        NodeViewModel result = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);
        NodeViewModel count = canvas.Nodes.First(n => n.Type == NodeType.CountStar);
        NodeViewModel comparison = canvas.Nodes.First(n =>
            n.Type is NodeType.Equals
                or NodeType.NotEquals
                or NodeType.GreaterThan
                or NodeType.GreaterOrEqual
                or NodeType.LessThan
                or NodeType.LessOrEqual);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == count
            && c.FromPin.Name.Equals("count", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == comparison
            && c.ToPin.Name.Equals("left", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == comparison
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("having", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertTopWiring(CanvasViewModel canvas)
    {
        NodeViewModel result = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput);
        NodeViewModel top = canvas.Nodes.First(n => n.Type == NodeType.Top);

        Assert.Contains(canvas.Connections, c =>
            c.FromPin.Owner == top
            && c.FromPin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
            && c.ToPin?.Owner == result
            && c.ToPin.Name.Equals("top", StringComparison.OrdinalIgnoreCase));
    }
}

using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SqlImporterScenarioMatrixTests
{
    [Fact]
    public void ScenarioMatrix_DefinesAtLeastSixtyImportCases()
    {
        Assert.True(ScenarioCases.Count >= 60, $"Expected at least 60 scenarios, found {ScenarioCases.Count}.");
    }

    [Theory]
    [MemberData(nameof(GetScenarioData))]
    public async Task ImportAsync_ScenarioMatrix_ProducesExpectedNodesAndWiring(SqlImportScenario scenario)
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);
        canvas.SqlImporter.SqlInput = scenario.Sql;

        await canvas.SqlImporter.ImportAsync();

        NodeViewModel resultNode = Assert.Single(canvas.Nodes, n => n.Type == NodeType.ResultOutput);
        Assert.True(canvas.Nodes.Count(n => n.IsTableSource) >= scenario.ExpectedSourceTables);

        if (scenario.ExpectsJoinNode)
            Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Join);

        if (scenario.ExpectsWhereComparison)
        {
            Assert.Contains(canvas.Nodes, n =>
                n.Type is NodeType.Equals or NodeType.NotEquals or NodeType.GreaterThan or NodeType.GreaterOrEqual or NodeType.LessThan or NodeType.LessOrEqual);
            Assert.Contains(
                canvas.Connections,
                c => string.Equals(c.FromPin.Name, "result", StringComparison.OrdinalIgnoreCase)
                    && c.ToPin?.Owner == resultNode
                    && string.Equals(c.ToPin.Name, "where", StringComparison.OrdinalIgnoreCase));
        }

        if (scenario.ExpectsWhereSubquery)
        {
            Assert.Contains(canvas.Nodes, n => n.Type is NodeType.SubqueryExists or NodeType.SubqueryIn or NodeType.SubqueryScalar);
            Assert.Contains(
                canvas.Connections,
                c => c.ToPin?.Owner == resultNode
                    && string.Equals(c.ToPin.Name, "where", StringComparison.OrdinalIgnoreCase));
        }

        if (scenario.ExpectsOrderByAsc)
        {
            Assert.Contains(
                canvas.Connections,
                c => c.ToPin?.Owner == resultNode
                    && string.Equals(c.ToPin.Name, "order_by", StringComparison.OrdinalIgnoreCase));
        }

        if (scenario.ExpectsOrderByDesc)
        {
            Assert.Contains(
                canvas.Connections,
                c => c.ToPin?.Owner == resultNode
                    && string.Equals(c.ToPin.Name, "order_by_desc", StringComparison.OrdinalIgnoreCase));
        }

        if (scenario.ExpectsGroupBy)
        {
            Assert.Contains(
                canvas.Connections,
                c => c.ToPin?.Owner == resultNode
                    && string.Equals(c.ToPin.Name, "group_by", StringComparison.OrdinalIgnoreCase));
        }

        if (scenario.ExpectsHaving)
        {
            Assert.Contains(
                canvas.Connections,
                c => c.ToPin?.Owner == resultNode
                    && string.Equals(c.ToPin.Name, "having", StringComparison.OrdinalIgnoreCase));
        }

        if (scenario.ExpectsLimitOrTop)
        {
            NodeViewModel topNode = Assert.Single(canvas.Nodes, n => n.Type == NodeType.Top);
            Assert.True(topNode.Parameters.ContainsKey("count"));
        }

        if (scenario.ExpectsDistinct)
            Assert.Equal("true", resultNode.Parameters.GetValueOrDefault("distinct"));
    }

    public static IEnumerable<object[]> GetScenarioData() =>
        ScenarioCases.Select(s => new object[] { s });

    private static readonly IReadOnlyList<SqlImportScenario> ScenarioCases = BuildScenarioMatrix();

    private static IReadOnlyList<SqlImportScenario> BuildScenarioMatrix()
    {
        var scenarios = new List<SqlImportScenario>(capacity: 60);

        string[] operators = ["=", "<>", "!=", ">", ">=", "<"];
        string[] values = ["100", "250", "400", "50", "75", "900"];

        // 30 scenarios: simple comparisons with clause combinations.
        for (int i = 0; i < operators.Length; i++)
        {
            string op = operators[i];
            string value = values[i];
            string comparisonBase = $"SELECT id, total, status FROM public.orders WHERE orders.total {op} {value}";

            scenarios.Add(new SqlImportScenario($"cmp_{i}_base", comparisonBase, 1, ExpectsWhereComparison: true));
            scenarios.Add(new SqlImportScenario($"cmp_{i}_order_asc", $"{comparisonBase} ORDER BY orders.status ASC", 1, ExpectsWhereComparison: true, ExpectsOrderByAsc: true));
            scenarios.Add(new SqlImportScenario($"cmp_{i}_order_desc", $"{comparisonBase} ORDER BY orders.total DESC", 1, ExpectsWhereComparison: true, ExpectsOrderByDesc: true));
            scenarios.Add(new SqlImportScenario($"cmp_{i}_limit", $"{comparisonBase} LIMIT 10", 1, ExpectsWhereComparison: true, ExpectsLimitOrTop: true));
            scenarios.Add(new SqlImportScenario($"cmp_{i}_distinct", $"SELECT DISTINCT id, total, status FROM public.orders WHERE orders.total {op} {value}", 1, ExpectsWhereComparison: true, ExpectsDistinct: true));
        }

        // 20 scenarios: join matrix with where/order/limit variants.
        string[] joins = ["INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "JOIN"];
        foreach (string join in joins)
        {
            string joinBase = $"SELECT orders.id, customers.name FROM public.orders {join} public.customers ON orders.customer_id = customers.id";

            scenarios.Add(new SqlImportScenario($"join_{join}_base", joinBase, 2, ExpectsJoinNode: true));
            scenarios.Add(new SqlImportScenario($"join_{join}_where", $"{joinBase} WHERE orders.id = 1", 2, ExpectsJoinNode: true, ExpectsWhereComparison: true));
            scenarios.Add(new SqlImportScenario($"join_{join}_order_desc", $"{joinBase} ORDER BY customers.name DESC", 2, ExpectsJoinNode: true, ExpectsOrderByDesc: true));
            scenarios.Add(new SqlImportScenario($"join_{join}_limit", $"{joinBase} LIMIT 25", 2, ExpectsJoinNode: true, ExpectsLimitOrTop: true));
        }

        // 10 scenarios: subqueries and grouping/having.
        string existsBase = "SELECT id FROM public.orders WHERE EXISTS (SELECT 1 FROM public.order_items oi WHERE oi.order_id = orders.id)";
        scenarios.Add(new SqlImportScenario("exists_base", existsBase, 1, ExpectsWhereSubquery: true));
        scenarios.Add(new SqlImportScenario("exists_order", $"{existsBase} ORDER BY orders.id ASC", 1, ExpectsWhereSubquery: true, ExpectsOrderByAsc: true));
        scenarios.Add(new SqlImportScenario("exists_limit", $"{existsBase} LIMIT 5", 1, ExpectsWhereSubquery: true, ExpectsLimitOrTop: true));

        string inBase = "SELECT id FROM public.orders WHERE orders.id IN (SELECT order_id FROM public.order_items)";
        scenarios.Add(new SqlImportScenario("in_base", inBase, 1, ExpectsWhereSubquery: true));
        scenarios.Add(new SqlImportScenario("in_order", $"{inBase} ORDER BY orders.id DESC", 1, ExpectsWhereSubquery: true, ExpectsOrderByDesc: true));
        scenarios.Add(new SqlImportScenario("in_limit", $"{inBase} LIMIT 8", 1, ExpectsWhereSubquery: true, ExpectsLimitOrTop: true));

        string scalarBase = "SELECT id FROM public.orders WHERE orders.total > (SELECT AVG(unit_price) FROM public.order_items)";
        scenarios.Add(new SqlImportScenario("scalar_base", scalarBase, 1, ExpectsWhereSubquery: true));
        scenarios.Add(new SqlImportScenario("scalar_order", $"{scalarBase} ORDER BY orders.id ASC", 1, ExpectsWhereSubquery: true, ExpectsOrderByAsc: true));
        scenarios.Add(new SqlImportScenario("scalar_limit", $"{scalarBase} LIMIT 3", 1, ExpectsWhereSubquery: true, ExpectsLimitOrTop: true));

        scenarios.Add(new SqlImportScenario(
            "group_having_order",
            "SELECT orders.status FROM public.orders GROUP BY orders.status HAVING COUNT(*) > 1 ORDER BY orders.status ASC",
            1,
            ExpectsGroupBy: true,
            ExpectsHaving: true,
            ExpectsOrderByAsc: true));

        Assert.Equal(60, scenarios.Count);
        return scenarios;
    }

    public sealed record SqlImportScenario(
        string Name,
        string Sql,
        int ExpectedSourceTables,
        bool ExpectsJoinNode = false,
        bool ExpectsWhereComparison = false,
        bool ExpectsWhereSubquery = false,
        bool ExpectsOrderByAsc = false,
        bool ExpectsOrderByDesc = false,
        bool ExpectsGroupBy = false,
        bool ExpectsHaving = false,
        bool ExpectsLimitOrTop = false,
        bool ExpectsDistinct = false)
    {
        public override string ToString() => Name;
    }
}

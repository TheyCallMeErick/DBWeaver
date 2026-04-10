using System.Text.RegularExpressions;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

public class SqlImportRoundTripMatrixIntegrationTests
{
    [Fact]
    public void RoundTripMatrix_DefinesAtLeastSeventyScenarios()
    {
        Assert.True(RoundTripScenarios.Count >= 70, $"Expected at least 70 scenarios, found {RoundTripScenarios.Count}.");
    }

    [Theory]
    [MemberData(nameof(GetRoundTripScenarioData))]
    public async Task RoundTrip_ImportGenerateReimport_ProducesStableGeneratedSql(RoundTripScenario scenario)
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);

        await ImportSqlWithAutoConfirmationAsync(canvas, scenario.Sql);
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        canvas.LiveSql.Recompile();
        string firstGeneratedSql = canvas.LiveSql.RawSql;
        Assert.False(string.IsNullOrWhiteSpace(firstGeneratedSql));
        Assert.DoesNotContain("-- Add a", firstGeneratedSql, StringComparison.OrdinalIgnoreCase);
        Assert.True(canvas.Nodes.Count(n => n.IsTableSource) >= scenario.ExpectedMinimumSources);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.ResultOutput);
        if (scenario.ExpectsJoin)
            Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Join);

        await ImportSqlWithAutoConfirmationAsync(canvas, firstGeneratedSql);
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        canvas.LiveSql.Recompile();
        string secondGeneratedSql = canvas.LiveSql.RawSql;
        Assert.False(string.IsNullOrWhiteSpace(secondGeneratedSql));

        string canonicalFirst = CanonicalizeSql(firstGeneratedSql);
        string canonicalSecond = CanonicalizeSql(secondGeneratedSql);
        Assert.Equal(canonicalFirst, canonicalSecond);
    }

    public static IEnumerable<object[]> GetRoundTripScenarioData() =>
        RoundTripScenarios.Select(scenario => new object[] { scenario });

    private static readonly IReadOnlyList<RoundTripScenario> RoundTripScenarios = BuildRoundTripScenarios();

    private static IReadOnlyList<RoundTripScenario> BuildRoundTripScenarios()
    {
        var scenarios = new List<RoundTripScenario>(capacity: 150);
        string[] operators = ["=", "<>", "!=", ">", ">=", "<", "<="];
        string[] joins = ["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN"];

        for (int i = 1; i <= 20; i++)
        {
            scenarios.Add(new RoundTripScenario(
                $"simple_limit_{i}",
                $"SELECT id FROM public.orders LIMIT {i}",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 0; i < 20; i++)
        {
            string op = operators[i % operators.Length];
            int value = 25 + (i * 10);
            scenarios.Add(new RoundTripScenario(
                $"where_order_{i}",
                $"SELECT id, customer_id FROM public.orders WHERE orders.id {op} {value} ORDER BY orders.id DESC LIMIT 50",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 0; i < 20; i++)
        {
            string join = joins[i % joins.Length];
            int limit = 10 + i;
            scenarios.Add(new RoundTripScenario(
                $"join_limit_{i}",
                $"SELECT orders.id FROM public.orders {join} public.customers ON orders.customer_id = customers.id ORDER BY orders.id ASC LIMIT {limit}",
                ExpectedMinimumSources: 2,
                ExpectsJoin: true));
        }

        for (int i = 0; i < 10; i++)
        {
            int havingValue = 1 + (i % 5);
            scenarios.Add(new RoundTripScenario(
                $"group_having_{i}",
                $"SELECT orders.status FROM public.orders GROUP BY orders.status HAVING COUNT(*) > {havingValue} ORDER BY orders.status ASC LIMIT 100",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 1; i <= 15; i++)
        {
            scenarios.Add(new RoundTripScenario(
                $"distinct_limit_{i}",
                $"SELECT DISTINCT customer_id FROM public.orders ORDER BY customer_id DESC LIMIT {i}",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 1; i <= 10; i++)
        {
            scenarios.Add(new RoundTripScenario(
                $"qualified_star_{i}",
                $"SELECT orders.* FROM public.orders ORDER BY orders.id ASC LIMIT {10 + i}",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 0; i < 15; i++)
        {
            scenarios.Add(new RoundTripScenario(
                $"exists_subquery_{i}",
                $"SELECT id FROM public.orders WHERE EXISTS (SELECT 1 FROM public.order_items WHERE order_items.order_id = orders.id) LIMIT {5 + i}",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 0; i < 10; i++)
        {
            scenarios.Add(new RoundTripScenario(
                $"in_subquery_{i}",
                $"SELECT id FROM public.orders WHERE id IN (SELECT order_id FROM public.order_items) ORDER BY id DESC LIMIT {20 + i}",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 0; i < 10; i++)
        {
            scenarios.Add(new RoundTripScenario(
                $"scalar_subquery_{i}",
                $"SELECT id FROM public.orders WHERE id > (SELECT MAX(id) FROM public.orders) LIMIT {30 + i}",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 0; i < 10; i++)
        {
            string conversionExpression = i % 2 == 0
                ? "CAST(total_amount AS TEXT) AS total_amount_text"
                : "COALESCE(status, 'unknown') AS normalized_status";
            scenarios.Add(new RoundTripScenario(
                $"transform_conversion_{i}",
                $"SELECT id, {conversionExpression} FROM public.orders WHERE id >= {1 + i} ORDER BY id ASC LIMIT {40 + i}",
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        for (int i = 0; i < 10; i++)
        {
            int limit = 5 + i;
            string sql = i < 5
                ? $"WITH src AS (SELECT id FROM public.orders) SELECT id FROM src ORDER BY id ASC LIMIT {limit}"
                : $"WITH src AS (SELECT id FROM public.orders), src2 AS (SELECT id FROM src) SELECT id FROM src2 ORDER BY id ASC LIMIT {limit}";

            scenarios.Add(new RoundTripScenario(
                $"cte_rewrite_{i}",
                sql,
                ExpectedMinimumSources: 1,
                ExpectsJoin: false));
        }

        Assert.Equal(150, scenarios.Count);
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

    public sealed record RoundTripScenario(
        string Name,
        string Sql,
        int ExpectedMinimumSources,
        bool ExpectsJoin)
    {
        public override string ToString() => Name;
    }
}

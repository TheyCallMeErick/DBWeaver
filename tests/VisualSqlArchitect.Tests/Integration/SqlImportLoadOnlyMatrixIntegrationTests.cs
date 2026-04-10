using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace Integration;

public class SqlImportLoadOnlyMatrixIntegrationTests
{
    [Fact]
    public void LoadOnlyMatrix_DefinesAtLeastThirtyScenarios()
    {
        Assert.True(LoadOnlyScenarios.Count >= 30, $"Expected at least 30 scenarios, found {LoadOnlyScenarios.Count}.");
    }

    [Theory]
    [MemberData(nameof(GetLoadOnlyScenarioData))]
    public async Task ImportOnly_Scenarios_LoadNodesAndCoreTopology(LoadOnlyScenario scenario)
    {
        var canvas = new CanvasViewModel();
        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(8);
        canvas.SqlImporter.SqlInput = scenario.Sql;

        await canvas.SqlImporter.ImportAsync();
        SqlImportWiringAssertions.AssertGraphWiringIfGraphExists(canvas);

        Assert.True(canvas.SqlImporter.HasReport);
        Assert.True(canvas.Nodes.Count >= scenario.MinimumNodes);
        Assert.True(canvas.Nodes.Count(n => n.IsTableSource) >= scenario.MinimumSources);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.ResultOutput);
        Assert.Contains(canvas.Nodes, n => n.Type == NodeType.ColumnSetBuilder);
        if (scenario.ExpectsJoin)
            Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Join);
        if (scenario.ExpectsTop)
            Assert.Contains(canvas.Nodes, n => n.Type == NodeType.Top);
    }

    public static IEnumerable<object[]> GetLoadOnlyScenarioData() =>
        LoadOnlyScenarios.Select(scenario => new object[] { scenario });

    private static readonly IReadOnlyList<LoadOnlyScenario> LoadOnlyScenarios = BuildLoadOnlyScenarios();

    private static IReadOnlyList<LoadOnlyScenario> BuildLoadOnlyScenarios()
    {
        var scenarios = new List<LoadOnlyScenario>(capacity: 30);
        string[] operators = ["=", "<>", "!=", ">", ">=", "<"];

        for (int i = 1; i <= 10; i++)
        {
            scenarios.Add(new LoadOnlyScenario(
                $"simple_star_{i}",
                "SELECT * FROM public.orders",
                MinimumNodes: 3,
                MinimumSources: 1,
                ExpectsJoin: false,
                ExpectsTop: false));
        }

        for (int i = 0; i < 10; i++)
        {
            string op = operators[i % operators.Length];
            int value = 10 + (i * 7);
            scenarios.Add(new LoadOnlyScenario(
                $"where_limit_{i}",
                $"SELECT id FROM public.orders WHERE orders.id {op} {value} LIMIT 100",
                MinimumNodes: 5,
                MinimumSources: 1,
                ExpectsJoin: false,
                ExpectsTop: true));
        }

        for (int i = 0; i < 10; i++)
        {
            scenarios.Add(new LoadOnlyScenario(
                $"join_{i}",
                "SELECT orders.id FROM public.orders JOIN public.customers ON orders.customer_id = customers.id",
                MinimumNodes: 5,
                MinimumSources: 2,
                ExpectsJoin: true,
                ExpectsTop: false));
        }

        Assert.Equal(30, scenarios.Count);
        return scenarios;
    }

    public sealed record LoadOnlyScenario(
        string Name,
        string Sql,
        int MinimumNodes,
        int MinimumSources,
        bool ExpectsJoin,
        bool ExpectsTop)
    {
        public override string ToString() => Name;
    }
}

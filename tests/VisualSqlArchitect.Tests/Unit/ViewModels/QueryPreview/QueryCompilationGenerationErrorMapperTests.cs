using DBWeaver.Nodes.LogicalPlan;
using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public sealed class QueryCompilationGenerationErrorMapperTests
{
    [Fact]
    public void Map_PlanningJoinWithoutCondition_ReturnsPlannerMessageAndGuidance()
    {
        var mapper = new QueryCompilationGenerationErrorMapper();
        var exception = new InvalidOperationException(
            "wrapper",
            new PlanningException("join_1", PlannerErrorKind.JoinWithoutCondition, "join missing condition"));

        List<string> mapped = mapper.Map(exception).ToList();

        Assert.Contains(mapped, m => m.Contains("join missing condition", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mapped, m => m.Contains("Join node requires an explicit condition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Map_PlanningOutputAmbiguous_ReturnsOutputGuidance()
    {
        var mapper = new QueryCompilationGenerationErrorMapper();
        var exception = new PlanningException(
            "out",
            PlannerErrorKind.OutputSourceAmbiguous,
            "multiple outputs");

        List<string> mapped = mapper.Map(exception).ToList();

        Assert.Contains(mapped, m => m.Contains("multiple outputs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mapped, m => m.Contains("exactly one top-level Result Output", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Map_PlanningCteNotReferenced_ReturnsCteGuidance()
    {
        var mapper = new QueryCompilationGenerationErrorMapper();
        var exception = new PlanningException(
            "out",
            PlannerErrorKind.CteNotReferencedInPlan,
            "cte_x not referenced");

        List<string> mapped = mapper.Map(exception).ToList();

        Assert.Contains(mapped, m => m.Contains("cte_x not referenced", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mapped, m => m.Contains("CTE definida sem uso", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Map_PlanningCyclicDependency_ReturnsCycleGuidance()
    {
        var mapper = new QueryCompilationGenerationErrorMapper();
        var exception = new PlanningException(
            "out",
            PlannerErrorKind.CyclicDependency,
            "cycle detected");

        List<string> mapped = mapper.Map(exception).ToList();

        Assert.Contains(mapped, m => m.Contains("cycle detected", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mapped, m => m.Contains("Dependência cíclica detectada", StringComparison.OrdinalIgnoreCase));
    }
}

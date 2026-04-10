using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public sealed record SqlServerParsedPlan(
    IReadOnlyList<ExplainNode> Nodes,
    double? PlanningTimeMs,
    double? ExecutionTimeMs
);




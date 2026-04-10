namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalOutput(
    LogicalNode Source,
    IReadOnlyList<LogicalCte> Ctes,
    IReadOnlyList<LogicalOrderBinding> OrderBy,
    bool Distinct,
    int? Limit,
    int? Offset) : LogicalNode;

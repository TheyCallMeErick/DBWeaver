namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalCte(
    string Name,
    LogicalNode Definition,
    bool Recursive = false) : LogicalNode;

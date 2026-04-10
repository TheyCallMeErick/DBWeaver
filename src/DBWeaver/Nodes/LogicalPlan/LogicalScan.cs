namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalScan(
    string NodeId,
    string Alias,
    string TableFullName,
    IReadOnlyList<LogicalColumn> Schema) : LogicalNode;

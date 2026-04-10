using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalFilter(
    string NodeId,
    LogicalNode Source,
    ISqlExpression Predicate) : LogicalNode;

using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalJoin(
    string NodeId,
    LogicalNode Left,
    LogicalNode Right,
    JoinKind Kind,
    ISqlExpression Condition) : LogicalNode;

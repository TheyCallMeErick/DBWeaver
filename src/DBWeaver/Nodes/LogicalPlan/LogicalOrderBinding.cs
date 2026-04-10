using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalOrderBinding(ISqlExpression Expression, bool Descending = false);

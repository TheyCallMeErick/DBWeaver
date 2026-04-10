using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record ProjectionBinding(ISqlExpression Expression, string? Alias = null);

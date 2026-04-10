using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record AggregateBinding(string Alias, ISqlExpression Expression);

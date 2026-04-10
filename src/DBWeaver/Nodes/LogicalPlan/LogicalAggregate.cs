using DBWeaver.Nodes;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed record LogicalAggregate(
    string NodeId,
    LogicalNode Source,
    IReadOnlyList<ISqlExpression> GroupByKeys,
    IReadOnlyList<AggregateBinding> Aggregates) : LogicalNode;

namespace DBWeaver.Nodes.LogicalPlan;

public sealed class PlanningException(string nodeId, PlannerErrorKind kind, string message)
    : Exception(message)
{
    public string NodeId { get; } = nodeId;
    public PlannerErrorKind Kind { get; } = kind;
}

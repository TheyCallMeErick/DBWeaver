namespace DBWeaver.Nodes.Pins;

public static class PinDisconnectionEvaluator
{
    public static IReadOnlyList<IPinMutation> EvaluateAfterDisconnect(
        PinModel source,
        PinModel destination,
        PinConnectionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(context);

        if (destination.Direction != PinDirection.Input
            || destination.EffectiveDataType != PinDataType.ColumnRef
            || !IsComparisonNode(destination.Owner.NodeType))
        {
            return [];
        }

        bool hasAnyConcretizingConnection = context.Data.ExistingConnections.Any(snapshot =>
            snapshot.DestinationNodeId == destination.Owner.NodeId
            && snapshot.DestinationEffectiveDataType == PinDataType.ColumnRef
            && snapshot.SourceResolvedScalarType is not null);

        if (hasAnyConcretizingConnection)
            return [];

        return [new ClearComparisonScalarMutation(destination.Owner.NodeId)];
    }

    private static bool IsComparisonNode(NodeType nodeType) =>
        nodeType is NodeType.Equals
            or NodeType.NotEquals
            or NodeType.GreaterThan
            or NodeType.GreaterOrEqual
            or NodeType.LessThan
            or NodeType.LessOrEqual
            or NodeType.Between
            or NodeType.NotBetween
            or NodeType.IsNull
            or NodeType.IsNotNull;
}

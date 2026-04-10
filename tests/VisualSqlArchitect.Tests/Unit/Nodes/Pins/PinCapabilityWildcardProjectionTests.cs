using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinCapabilityWildcardProjectionTests
{
    [Fact]
    public void WildcardProjection_EmitsPruneMutationForSameSourceProjectionConnections()
    {
        PinModel sourceWildcard = CreatePin("*", PinDirection.Output, PinDataType.ColumnSet, NodeType.TableSource);
        PinModel destination = CreatePin("columns", PinDirection.Input, PinDataType.ColumnRef, NodeType.ColumnList, destinationNodeId: "projection-1");

        var redundant = new PinConnectionSnapshot(
            ConnectionId: "wire-old",
            SourcePinId: new PinId("table-a:id:Output"),
            DestinationPinId: new PinId("projection-1:columns:Input"),
            SourceNodeId: sourceWildcard.Owner.NodeId,
            DestinationNodeId: destination.Owner.NodeId,
            SourcePinName: "id",
            DestinationPinName: "columns",
            SourceEffectiveDataType: PinDataType.ColumnRef,
            DestinationEffectiveDataType: PinDataType.ColumnRef,
            SourceResolvedScalarType: PinDataType.Number);

        var unrelated = new PinConnectionSnapshot(
            ConnectionId: "wire-keep",
            SourcePinId: new PinId("table-b:id:Output"),
            DestinationPinId: new PinId("projection-1:columns:Input"),
            SourceNodeId: "other-table",
            DestinationNodeId: destination.Owner.NodeId,
            SourcePinName: "id",
            DestinationPinName: "columns",
            SourceEffectiveDataType: PinDataType.ColumnRef,
            DestinationEffectiveDataType: PinDataType.ColumnRef,
            SourceResolvedScalarType: PinDataType.Number);

        var contextData = new PinConnectionContextData(
            ExistingConnections: [redundant, unrelated],
            ConnectionsByPin: new Dictionary<PinId, PinConnectionSnapshot[]>(),
            IsValidationOnly: true,
            AllowImplicitReplacement: true,
            ComparisonState: null,
            WildcardContext: null);

        PinConnectionDecision decision = destination.CanConnect(sourceWildcard, new PinConnectionContext(contextData, new Dictionary<string, object?>()));

        Assert.True(decision.IsAllowed);
        PruneConnectionsMutation pruneMutation = Assert.IsType<PruneConnectionsMutation>(
            decision.Mutations.Single(m => m is PruneConnectionsMutation));
        Assert.Contains("wire-old", pruneMutation.PrunedConnectionIds);
        Assert.DoesNotContain("wire-keep", pruneMutation.PrunedConnectionIds);
    }

    private static PinModel CreatePin(
        string pinName,
        PinDirection direction,
        PinDataType dataType,
        NodeType nodeType,
        string? destinationNodeId = null)
    {
        string nodeId = destinationNodeId ?? $"{nodeType}:{Guid.NewGuid():N}";
        var descriptor = new PinDescriptor(pinName, direction, dataType);
        var owner = new PinModelOwner(nodeId, nodeType);
        var pinId = new PinId($"{nodeId}:{pinName}:{direction}");
        return PinModelFactory.Create(pinId, descriptor, owner, dataType, null);
    }
}

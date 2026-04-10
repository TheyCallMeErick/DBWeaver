using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinMultiplicityPolicyTests
{
    [Fact]
    public void SingleConnectionDestination_ProposesReplacementMutation_WhenImplicitReplacementEnabled()
    {
        PinModel sourceA = CreatePin("a", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel sourceB = CreatePin("b", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel destination = CreatePin("value", PinDirection.Input, PinDataType.Number, NodeType.Sum, allowMultiple: false);

        var existing = new PinConnectionSnapshot(
            ConnectionId: "wire-1",
            SourcePinId: sourceA.PinId,
            DestinationPinId: destination.PinId,
            SourceNodeId: sourceA.Owner.NodeId,
            DestinationNodeId: destination.Owner.NodeId,
            SourcePinName: sourceA.Name,
            DestinationPinName: destination.Name,
            SourceEffectiveDataType: sourceA.EffectiveDataType,
            DestinationEffectiveDataType: destination.EffectiveDataType,
            SourceResolvedScalarType: sourceA.EffectiveDataType);

        var contextData = new PinConnectionContextData(
            ExistingConnections: [existing],
            ConnectionsByPin: new Dictionary<PinId, PinConnectionSnapshot[]>
            {
                [destination.PinId] = [existing],
                [sourceA.PinId] = [existing],
            },
            IsValidationOnly: true,
            AllowImplicitReplacement: true,
            ComparisonState: null,
            WildcardContext: null);

        PinConnectionDecision decision = destination.CanConnect(sourceB, new PinConnectionContext(contextData, new Dictionary<string, object?>()));

        Assert.True(decision.IsAllowed);
        ReplaceExistingConnectionMutation mutation = Assert.IsType<ReplaceExistingConnectionMutation>(Assert.Single(decision.Mutations));
        Assert.Contains("wire-1", mutation.ReplacedConnectionIds);
    }

    private static PinModel CreatePin(string pinName, PinDirection direction, PinDataType dataType, NodeType nodeType, bool allowMultiple = false)
    {
        string nodeId = $"{nodeType}:{Guid.NewGuid():N}";
        var descriptor = new PinDescriptor(pinName, direction, dataType, AllowMultiple: allowMultiple);
        var owner = new PinModelOwner(nodeId, nodeType);
        var pinId = new PinId($"{nodeId}:{pinName}:{direction}");
        return PinModelFactory.Create(pinId, descriptor, owner, dataType, null);
    }
}

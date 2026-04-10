using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinCapabilityComparisonConcretizationTests
{
    [Fact]
    public void ComparisonInputConnection_EmitsConcretizeMutation()
    {
        PinModel source = CreatePin("value", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel destination = CreatePin("left", PinDirection.Input, PinDataType.ColumnRef, NodeType.Equals);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.True(decision.IsAllowed);
        ConcretizeComparisonScalarMutation mutation = Assert.IsType<ConcretizeComparisonScalarMutation>(
            decision.Mutations.Single(m => m is ConcretizeComparisonScalarMutation));
        Assert.Equal(destination.Owner.NodeId, mutation.NodeId);
        Assert.Equal(PinDataType.Number, mutation.ScalarType);
    }

    [Fact]
    public void NonComparisonDestination_DoesNotEmitConcretizeMutation()
    {
        PinModel source = CreatePin("value", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel destination = CreatePin("value", PinDirection.Input, PinDataType.ColumnRef, NodeType.Sum);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.True(decision.IsAllowed);
        Assert.DoesNotContain(decision.Mutations, m => m is ConcretizeComparisonScalarMutation);
    }

    private static PinModel CreatePin(string pinName, PinDirection direction, PinDataType dataType, NodeType nodeType)
    {
        string nodeId = $"{nodeType}:{Guid.NewGuid():N}";
        var descriptor = new PinDescriptor(pinName, direction, dataType);
        var owner = new PinModelOwner(nodeId, nodeType);
        var pinId = new PinId($"{nodeId}:{pinName}:{direction}");
        return PinModelFactory.Create(pinId, descriptor, owner, dataType, null);
    }
}

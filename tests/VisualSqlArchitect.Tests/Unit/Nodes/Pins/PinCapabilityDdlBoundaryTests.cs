using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinCapabilityDdlBoundaryTests
{
    [Fact]
    public void QueryScalarToDdlPin_IsRejectedWithDomainFamilyMismatch()
    {
        PinModel source = CreatePin("value", PinDirection.Output, PinDataType.Number, NodeType.Sum);
        PinModel destination = CreatePin("table", PinDirection.Input, PinDataType.TableDef, NodeType.DropTableOp);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.False(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.DomainFamilyMismatch, decision.ReasonCode);
    }

    [Fact]
    public void DdlPinToQueryScalar_IsRejectedWithDomainFamilyMismatch()
    {
        PinModel source = CreatePin("table", PinDirection.Output, PinDataType.TableDef, NodeType.TableDefinition);
        PinModel destination = CreatePin("value", PinDirection.Input, PinDataType.Number, NodeType.Sum);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.False(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.DomainFamilyMismatch, decision.ReasonCode);
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

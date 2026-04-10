using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinCapabilityStructuralCompatibilityTests
{
    [Fact]
    public void RowSetToScalar_IsRejectedAsStructuralMismatch()
    {
        PinModel source = CreatePin("result", PinDirection.Output, PinDataType.RowSet, NodeType.Subquery);
        PinModel destination = CreatePin("value", PinDirection.Input, PinDataType.Number, NodeType.Sum);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.False(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.StructuralTypeMismatch, decision.ReasonCode);
    }

    [Fact]
    public void TableWildcardToColumnListColumns_IsAllowedWithWildcardReasonCode()
    {
        PinModel source = CreatePin("*", PinDirection.Output, PinDataType.ColumnSet, NodeType.TableSource);
        PinModel destination = CreatePin("columns", PinDirection.Input, PinDataType.ColumnRef, NodeType.ColumnList);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.True(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.WildcardProjectionOnly, decision.ReasonCode);
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

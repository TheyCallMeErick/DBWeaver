using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinEffectiveDataTypeResolutionTests
{
    [Fact]
    public void ColumnRefComparison_UsesExpectedScalarType_WhenMetadataIsUnavailable()
    {
        PinModel source = CreateColumnRefPin(
            pinName: "left",
            direction: PinDirection.Output,
            nodeType: NodeType.ValueNumber,
            expectedScalarType: PinDataType.Integer);
        PinModel destination = CreateColumnRefPin(
            pinName: "right",
            direction: PinDirection.Input,
            nodeType: NodeType.Equals,
            expectedScalarType: PinDataType.Integer);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void ColumnRefComparison_RejectsWhenExpectedScalarTypesDiffer()
    {
        PinModel source = CreateColumnRefPin(
            pinName: "left",
            direction: PinDirection.Output,
            nodeType: NodeType.ValueNumber,
            expectedScalarType: PinDataType.Integer);
        PinModel destination = CreateColumnRefPin(
            pinName: "right",
            direction: PinDirection.Input,
            nodeType: NodeType.Equals,
            expectedScalarType: PinDataType.Decimal);

        PinConnectionDecision decision = destination.CanConnect(source, PinConnectionContext.ValidationOnly());

        Assert.False(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.ComparisonTypeMismatch, decision.ReasonCode);
    }

    private static PinModel CreateColumnRefPin(
        string pinName,
        PinDirection direction,
        NodeType nodeType,
        PinDataType expectedScalarType)
    {
        string nodeId = $"{nodeType}:{Guid.NewGuid():N}";
        var descriptor = new PinDescriptor(pinName, direction, PinDataType.ColumnRef);
        var owner = new PinModelOwner(nodeId, nodeType);
        var pinId = new PinId($"{nodeId}:{pinName}:{direction}");

        return PinModelFactory.Create(
            pinId,
            descriptor,
            owner,
            PinDataType.ColumnRef,
            expectedScalarType);
    }
}

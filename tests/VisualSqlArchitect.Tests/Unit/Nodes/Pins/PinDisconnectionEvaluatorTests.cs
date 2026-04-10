using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinDisconnectionEvaluatorTests
{
    [Fact]
    public void EvaluateAfterDisconnect_EmitsClearMutation_WhenNoConcretizingConnectionsRemain()
    {
        PinModel source = CreatePin("value", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel destination = CreatePin("left", PinDirection.Input, PinDataType.ColumnRef, NodeType.Equals, nodeId: "cmp-1");

        var context = new PinConnectionContext(
            new PinConnectionContextData(
                ExistingConnections: [],
                ConnectionsByPin: new Dictionary<PinId, PinConnectionSnapshot[]>(),
                IsValidationOnly: true,
                AllowImplicitReplacement: false,
                ComparisonState: null,
                WildcardContext: null),
            new Dictionary<string, object?>());

        IReadOnlyList<IPinMutation> mutations = PinDisconnectionEvaluator.EvaluateAfterDisconnect(source, destination, context);

        ClearComparisonScalarMutation clear = Assert.IsType<ClearComparisonScalarMutation>(Assert.Single(mutations));
        Assert.Equal("cmp-1", clear.NodeId);
    }

    [Fact]
    public void EvaluateAfterDisconnect_DoesNotEmitClearMutation_WhenConcretizingConnectionRemains()
    {
        PinModel source = CreatePin("value", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel destination = CreatePin("left", PinDirection.Input, PinDataType.ColumnRef, NodeType.Equals, nodeId: "cmp-1");

        var remaining = new PinConnectionSnapshot(
            ConnectionId: "wire-remaining",
            SourcePinId: new PinId("src-2:value:Output"),
            DestinationPinId: new PinId("cmp-1:right:Input"),
            SourceNodeId: "src-2",
            DestinationNodeId: "cmp-1",
            SourcePinName: "value",
            DestinationPinName: "right",
            SourceEffectiveDataType: PinDataType.Number,
            DestinationEffectiveDataType: PinDataType.ColumnRef,
            SourceResolvedScalarType: PinDataType.Number);

        var context = new PinConnectionContext(
            new PinConnectionContextData(
                ExistingConnections: [remaining],
                ConnectionsByPin: new Dictionary<PinId, PinConnectionSnapshot[]>(),
                IsValidationOnly: true,
                AllowImplicitReplacement: false,
                ComparisonState: null,
                WildcardContext: null),
            new Dictionary<string, object?>());

        IReadOnlyList<IPinMutation> mutations = PinDisconnectionEvaluator.EvaluateAfterDisconnect(source, destination, context);

        Assert.Empty(mutations);
    }

    [Fact]
    public void EvaluateAfterDisconnect_DoesNotEmitClearMutation_WhenDestinationIsNotComparisonNode()
    {
        PinModel source = CreatePin("value", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel destination = CreatePin("value", PinDirection.Input, PinDataType.ColumnRef, NodeType.Sum, nodeId: "sum-1");

        var context = new PinConnectionContext(
            new PinConnectionContextData(
                ExistingConnections: [],
                ConnectionsByPin: new Dictionary<PinId, PinConnectionSnapshot[]>(),
                IsValidationOnly: true,
                AllowImplicitReplacement: false,
                ComparisonState: null,
                WildcardContext: null),
            new Dictionary<string, object?>());

        IReadOnlyList<IPinMutation> mutations = PinDisconnectionEvaluator.EvaluateAfterDisconnect(source, destination, context);

        Assert.Empty(mutations);
    }

    [Fact]
    public void EvaluateAfterDisconnect_EmitsClearMutation_ForIsNullComparisonNode()
    {
        PinModel source = CreatePin("value", PinDirection.Output, PinDataType.Number, NodeType.ValueNumber);
        PinModel destination = CreatePin("value", PinDirection.Input, PinDataType.ColumnRef, NodeType.IsNull, nodeId: "cmp-null-1");

        var context = new PinConnectionContext(
            new PinConnectionContextData(
                ExistingConnections: [],
                ConnectionsByPin: new Dictionary<PinId, PinConnectionSnapshot[]>(),
                IsValidationOnly: true,
                AllowImplicitReplacement: false,
                ComparisonState: null,
                WildcardContext: null),
            new Dictionary<string, object?>());

        IReadOnlyList<IPinMutation> mutations = PinDisconnectionEvaluator.EvaluateAfterDisconnect(source, destination, context);

        ClearComparisonScalarMutation clear = Assert.IsType<ClearComparisonScalarMutation>(Assert.Single(mutations));
        Assert.Equal("cmp-null-1", clear.NodeId);
    }

    private static PinModel CreatePin(string pinName, PinDirection direction, PinDataType dataType, NodeType nodeType, string? nodeId = null)
    {
        string ownerNodeId = nodeId ?? $"{nodeType}:{Guid.NewGuid():N}";
        var descriptor = new PinDescriptor(pinName, direction, dataType);
        var owner = new PinModelOwner(ownerNodeId, nodeType);
        var pinId = new PinId($"{ownerNodeId}:{pinName}:{direction}");
        return PinModelFactory.Create(pinId, descriptor, owner, dataType, null);
    }
}

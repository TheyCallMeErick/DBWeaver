using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;
using DBWeaver.UI.Serialization;
using DBWeaver.UI.Services.Validation;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinFinalCrossRegressionTests
{
    [Fact]
    public void DomainDecisionAndCanvasCommit_AgreeOnAcceptedConnection()
    {
        PinModel sourceModel = CreatePinModel("source", PinDirection.Output, PinDataType.Integer, NodeType.ValueNumber);
        PinModel destinationModel = CreatePinModel("target", PinDirection.Input, PinDataType.Number, NodeType.Sum);
        PinConnectionDecision decision = destinationModel.CanConnect(sourceModel, PinConnectionContext.ValidationOnly());

        Assert.True(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.None, decision.ReasonCode);

        using var canvas = new CanvasViewModel();
        var sourceNode = new NodeViewModel("public.orders", [("source", PinDataType.Integer)], new Point(0, 0));
        var sumNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));
        canvas.Nodes.Add(sourceNode);
        canvas.Nodes.Add(sumNode);

        PinViewModel from = sourceNode.OutputPins.Single(p => p.Name == "source");
        PinViewModel to = sumNode.InputPins.Single(p => p.Name == "value");
        canvas.ConnectPins(from, to);

        Assert.Single(canvas.Connections, c => c.FromPin == from && c.ToPin == to);
    }

    [Fact]
    public void InvalidWire_IsExplainedByReasonCatalogThroughValidator()
    {
        using var canvas = new CanvasViewModel();
        var sourceNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Subquery), new Point(0, 0));
        var sumNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));
        canvas.Nodes.Add(sourceNode);
        canvas.Nodes.Add(sumNode);

        PinViewModel from = sourceNode.OutputPins.Single(p => p.Name == "result");
        PinViewModel to = sumNode.InputPins.Single(p => p.Name == "value");
        canvas.Connections.Add(new ConnectionViewModel(from, from.AbsolutePosition, to.AbsolutePosition) { ToPin = to });

        ValidationIssue issue = Assert.Single(GraphValidator.Validate(canvas), i => i.Code == "STRUCTURAL_TYPE_MISMATCH");
        Assert.Equal("STRUCTURAL_TYPE_MISMATCH", PinConnectionReasonCatalog.ToIssueCode(PinConnectionReasonCode.StructuralTypeMismatch));
        Assert.Contains("structural", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AcceptedConnection_RoundTripsThroughSerializer_WithStableEndpoints()
    {
        using var sourceCanvas = new CanvasViewModel();
        var table = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var sum = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));
        sourceCanvas.Nodes.Add(table);
        sourceCanvas.Nodes.Add(sum);
        sourceCanvas.ConnectPins(
            table.OutputPins.Single(p => p.Name == "id"),
            sum.InputPins.Single(p => p.Name == "value"));

        string json = CanvasSerializer.Serialize(sourceCanvas);

        using var loaded = new CanvasViewModel();
        CanvasLoadResult result = CanvasSerializer.Deserialize(json, loaded);

        Assert.True(result.Success);
        ConnectionViewModel restored = Assert.Single(loaded.Connections);
        Assert.Equal("id", restored.FromPin.Name);
        Assert.Equal("value", restored.ToPin?.Name);
    }

    private static PinModel CreatePinModel(
        string pinName,
        PinDirection direction,
        PinDataType dataType,
        NodeType nodeType)
    {
        string nodeId = $"{nodeType}:{Guid.NewGuid():N}";
        var descriptor = new PinDescriptor(pinName, direction, dataType);
        var owner = new PinModelOwner(nodeId, nodeType);
        var pinId = new PinId($"{nodeId}:{pinName}:{direction}");
        return PinModelFactory.Create(pinId, descriptor, owner, dataType, null);
    }
}

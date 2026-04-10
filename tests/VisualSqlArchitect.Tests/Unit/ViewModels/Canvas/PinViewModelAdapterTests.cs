using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.Nodes.Pins;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public sealed class PinViewModelAdapterTests
{
    [Fact]
    public void EvaluateConnection_ReturnsReasonCodeFromDomain_WhenRejected()
    {
        var sourceNode = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        var destinationNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(100, 0));

        PinViewModel source = sourceNode.OutputPins.First(p => p.Name == "*");
        PinViewModel destination = destinationNode.InputPins.First(p => p.Name == "value");

        PinConnectionDecision decision = destination.EvaluateConnection(source);

        Assert.False(decision.IsAllowed);
        Assert.Equal(PinConnectionReasonCode.ScalarTypeMismatch, decision.ReasonCode);
    }

    [Fact]
    public void CanAccept_UsesDomainDecisionOutcome()
    {
        var sourceNode = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var destinationNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(100, 0));

        PinViewModel source = sourceNode.OutputPins.First(p => p.Name == "id");
        PinViewModel destination = destinationNode.InputPins.First(p => p.Name == "value");

        PinConnectionDecision decision = destination.EvaluateConnection(source);

        Assert.True(decision.IsAllowed);
        Assert.True(destination.CanAccept(source));
    }
}

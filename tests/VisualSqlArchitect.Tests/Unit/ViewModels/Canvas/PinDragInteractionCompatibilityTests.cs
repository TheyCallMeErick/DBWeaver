using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public sealed class PinDragInteractionCompatibilityTests
{
    [Fact]
    public void Constructor_FlagsOnlyDomainCompatiblePinsAsDropTargets()
    {
        var sourceNode = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var sumNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));
        var rowSetJoinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.RowSetJoin), new Point(220, 120));

        PinViewModel source = sourceNode.OutputPins.Single(p => p.Name == "id");
        PinViewModel validTarget = sumNode.InputPins.Single(p => p.Name == "value");
        PinViewModel invalidTarget = rowSetJoinNode.InputPins.Single(p => p.Name == "left");

        var liveWire = new ConnectionViewModel(source, source.AbsolutePosition, source.AbsolutePosition);
        var state = new PinDragState(
            source,
            liveWire,
            [source, validTarget, invalidTarget]);

        Assert.Contains(validTarget, state.ValidTargets);
        Assert.DoesNotContain(invalidTarget, state.ValidTargets);
        Assert.True(validTarget.IsDropTarget);
        Assert.True(invalidTarget.IsDragIncompatible);

        state.Cancel();

        Assert.False(validTarget.IsDropTarget);
        Assert.False(invalidTarget.IsDragIncompatible);
    }
}

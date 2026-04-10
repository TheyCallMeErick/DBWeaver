using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public sealed class PinManagerDomainOrchestrationTests
{
    [Fact]
    public void ConnectPins_WhenDestinationIsSingleConnection_ReplacesExistingConnectionAsSingleUndoableOperation()
    {
        using var canvas = new CanvasViewModel();

        var sourceA = new NodeViewModel("public.orders", [("id_a", PinDataType.Integer)], new Point(0, 0));
        var sourceB = new NodeViewModel("public.orders", [("id_b", PinDataType.Integer)], new Point(0, 120));
        var sum = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Sum), new Point(220, 0));

        canvas.Nodes.Add(sourceA);
        canvas.Nodes.Add(sourceB);
        canvas.Nodes.Add(sum);

        PinViewModel destination = sum.InputPins.Single(p => p.Name == "value");
        PinViewModel fromA = sourceA.OutputPins.Single(p => p.Name == "id_a");
        PinViewModel fromB = sourceB.OutputPins.Single(p => p.Name == "id_b");

        canvas.ConnectPins(fromA, destination);
        canvas.ConnectPins(fromB, destination);

        ConnectionViewModel current = Assert.Single(canvas.Connections, c => c.ToPin == destination);
        Assert.Equal("id_b", current.FromPin.Name);

        canvas.UndoRedo.Undo();

        ConnectionViewModel restored = Assert.Single(canvas.Connections, c => c.ToPin == destination);
        Assert.Equal("id_a", restored.FromPin.Name);
    }

    [Fact]
    public void DeleteConnection_WhenComparisonConcretizationBecomesUnsustained_IsUndoable()
    {
        using var canvas = new CanvasViewModel();

        var source = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var equals = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(240, 0));

        canvas.Nodes.Add(source);
        canvas.Nodes.Add(equals);

        PinViewModel from = source.OutputPins.Single(p => p.Name == "id");
        PinViewModel left = equals.InputPins.Single(p => p.Name == "left");
        PinViewModel right = equals.InputPins.Single(p => p.Name == "right");

        canvas.ConnectPins(from, left);
        Assert.Equal(PinDataType.Integer, left.ExpectedColumnScalarType);
        Assert.Equal(PinDataType.Integer, right.ExpectedColumnScalarType);

        ConnectionViewModel existing = Assert.Single(canvas.Connections, c => c.ToPin == left);
        canvas.DeleteConnection(existing);

        Assert.Null(left.ExpectedColumnScalarType);
        Assert.Null(right.ExpectedColumnScalarType);

        canvas.UndoRedo.Undo();

        Assert.Equal(PinDataType.Integer, left.ExpectedColumnScalarType);
        Assert.Equal(PinDataType.Integer, right.ExpectedColumnScalarType);
        Assert.Single(canvas.Connections, c => c.ToPin == left);
    }
}

using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class ComparisonPinConcretizationTests
{
    [Fact]
    public void ConnectPins_ComparisonNode_FirstWireConcretizesSiblingInputs()
    {
        var canvas = new CanvasViewModel();
        var orders = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var equals = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(240, 0));

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(equals);

        PinViewModel source = orders.OutputPins.First(p => p.Name == "id");
        PinViewModel left = equals.InputPins.First(p => p.Name == "left");
        PinViewModel right = equals.InputPins.First(p => p.Name == "right");

        canvas.ConnectPins(source, left);

        Assert.Equal(PinDataType.Integer, left.ExpectedColumnScalarType);
        Assert.Equal(PinDataType.Integer, right.ExpectedColumnScalarType);
    }

    [Fact]
    public void DeleteConnection_ComparisonNode_ClearsConcretizedTypeWhenNoInputsRemain()
    {
        var canvas = new CanvasViewModel();
        var orders = new NodeViewModel("public.orders", [("id", PinDataType.Integer)], new Point(0, 0));
        var equals = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(240, 0));

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(equals);

        PinViewModel source = orders.OutputPins.First(p => p.Name == "id");
        PinViewModel left = equals.InputPins.First(p => p.Name == "left");

        canvas.ConnectPins(source, left);

        ConnectionViewModel connection = canvas.Connections.Single(c => c.ToPin == left);
        canvas.DeleteConnection(connection);

        Assert.Null(equals.InputPins.First(p => p.Name == "left").ExpectedColumnScalarType);
        Assert.Null(equals.InputPins.First(p => p.Name == "right").ExpectedColumnScalarType);
    }
}

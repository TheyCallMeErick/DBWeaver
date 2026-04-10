using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class PinManagerNarrowingConsistencyTests
{
    [Fact]
    public void EffectiveDataType_RemainsDeclaredType_WhenConnectionsChange()
    {
        var canvas = new CanvasViewModel();

        var sourceTable = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        var equalsNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(200, 0));

        canvas.Nodes.Add(sourceTable);
        canvas.Nodes.Add(equalsNode);

        var sourcePin = sourceTable.OutputPins.First(p => p.Name == "id");
        var anyPin = equalsNode.InputPins.First(p => p.Name == "left");

        var conn = new ConnectionViewModel(sourcePin, sourcePin.AbsolutePosition, anyPin.AbsolutePosition)
        {
            ToPin = anyPin,
        };

        canvas.Connections.Add(conn);
        Assert.Equal(anyPin.DataType, anyPin.EffectiveDataType);

        canvas.Connections.Remove(conn);
        Assert.Equal(anyPin.DataType, anyPin.EffectiveDataType);
    }
}


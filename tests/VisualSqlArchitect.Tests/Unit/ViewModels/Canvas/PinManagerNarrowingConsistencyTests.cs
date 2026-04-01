using Avalonia;
using System.Reflection;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class PinManagerNarrowingConsistencyTests
{
    [Fact]
    public void ClearNarrowingIfNeeded_DoesNotMutateLegacyNarrowingState()
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
        anyPin.NarrowedDataType = PinDataType.Number;

        MethodInfo clearMethod = typeof(CanvasViewModel)
            .GetMethod("ClearNarrowingIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic)!;

        clearMethod.Invoke(canvas, [new[] { equalsNode }]);
        Assert.Equal(PinDataType.Number, anyPin.NarrowedDataType);

        canvas.Connections.Remove(conn);
        clearMethod.Invoke(canvas, [new[] { equalsNode }]);
        Assert.Equal(PinDataType.Number, anyPin.NarrowedDataType);
    }
}

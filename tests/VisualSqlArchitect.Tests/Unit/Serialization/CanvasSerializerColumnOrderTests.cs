using System.Reflection;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Serialization;

public class CanvasSerializerColumnOrderTests
{
    [Fact]
    public void ApplySavedColumnOrder_ReordersDeterministically()
    {
        var def = NodeDefinitionRegistry.Get(NodeType.ResultOutput);
        var node = new NodeViewModel(def, new Point(0, 0));

        node.OutputColumnOrder.Add(new OutputColumnEntry("A", "A", () => { }, () => { }));
        node.OutputColumnOrder.Add(new OutputColumnEntry("B", "B", () => { }, () => { }));
        node.OutputColumnOrder.Add(new OutputColumnEntry("C", "C", () => { }, () => { }));

        MethodInfo method = typeof(CanvasSerializer)
            .GetMethod("ApplySavedColumnOrder", BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(null, [node, new[] { "C", "A", "B" }]);

        Assert.Equal(["C", "A", "B"], node.OutputColumnOrder.Select(x => x.Key).ToArray());
    }
}

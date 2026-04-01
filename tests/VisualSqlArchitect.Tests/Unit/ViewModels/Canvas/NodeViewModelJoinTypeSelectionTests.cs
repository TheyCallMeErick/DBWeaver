using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class NodeViewModelJoinTypeSelectionTests
{
    [Fact]
    public void JoinTypeSelection_Setter_UpdatesJoinTypeParameter()
    {
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));

        node.JoinTypeSelection = "RIGHT";

        Assert.Equal("RIGHT", node.Parameters["join_type"]);
        Assert.False(node.Parameters.ContainsKey("type"));
    }
}

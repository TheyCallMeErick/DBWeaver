using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

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



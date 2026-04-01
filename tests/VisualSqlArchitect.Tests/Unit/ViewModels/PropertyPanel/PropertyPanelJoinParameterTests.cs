using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelJoinParameterTests
{
    [Fact]
    public void ShowNode_JoinNode_ShowsJoinTypeAndDoesNotShowTypeParameter()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);

        var joinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));

        panel.ShowNode(joinNode);

        Assert.Contains(panel.Parameters, p => p.Name == "join_type");
        Assert.DoesNotContain(panel.Parameters, p => p.Name == "type");
    }

    [Fact]
    public void CommitDirty_JoinTypeChange_UpdatesJoinTypeOnly()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);

        var joinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));
        panel.ShowNode(joinNode);

        var joinTypeRow = panel.Parameters.First(p => p.Name == "join_type");
        joinTypeRow.Value = "RIGHT";
        panel.CommitDirty();

        Assert.Equal("RIGHT", joinNode.Parameters["join_type"]);
        Assert.False(joinNode.Parameters.ContainsKey("type"));
    }
}

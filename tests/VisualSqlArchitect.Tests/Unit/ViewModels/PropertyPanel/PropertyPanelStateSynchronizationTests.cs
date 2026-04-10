using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.UndoRedo.Commands;

namespace DBWeaver.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelStateSynchronizationTests
{
    [Fact]
    public void SelectedParameterRow_ReflectsExternalNodeParameterMutation()
    {
        var canvas = new CanvasViewModel();
        var joinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));
        canvas.Nodes.Add(joinNode);
        canvas.PropertyPanel.ShowNode(joinNode);

        ParameterRowViewModel joinTypeRow = canvas.PropertyPanel.Parameters.First(parameter =>
            string.Equals(parameter.Name, "join_type", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("INNER", joinTypeRow.Value);

        joinNode.Parameters["join_type"] = "LEFT";
        joinNode.RaiseParameterChanged("join_type");

        Assert.Equal("LEFT", joinTypeRow.Value);
        Assert.False(joinTypeRow.IsDirty);
    }

    [Fact]
    public void SelectedParameterRow_TracksUndoRedoStateChanges()
    {
        var canvas = new CanvasViewModel();
        var joinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));
        canvas.Nodes.Add(joinNode);
        canvas.PropertyPanel.ShowNode(joinNode);

        canvas.UndoRedo.Execute(new EditParameterCommand(
            joinNode,
            "join_type",
            oldValue: joinNode.Parameters["join_type"],
            newValue: "RIGHT"));

        ParameterRowViewModel joinTypeRow = canvas.PropertyPanel.Parameters.First(parameter =>
            string.Equals(parameter.Name, "join_type", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("RIGHT", joinNode.Parameters["join_type"]);
        Assert.Equal("RIGHT", joinTypeRow.Value);

        canvas.UndoRedo.Undo();
        Assert.Equal("INNER", joinNode.Parameters["join_type"]);
        Assert.Equal("INNER", joinTypeRow.Value);
    }
}

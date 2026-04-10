using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using System.Collections.ObjectModel;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class SelectionManagerCanExecuteTests
{
    private static NodeViewModel Node(string name) =>
        new(name, [("id", PinDataType.Number)], new Point(0, 0));

    [Fact]
    public void AlignCommands_RequireAtLeastTwoSelectedNodes()
    {
        var nodes = new ObservableCollection<NodeViewModel>
        {
            Node("a"),
            Node("b"),
            Node("c")
        };

        var undo = new UndoRedoStack(new CanvasViewModel());
        var panel = new PropertyPanelViewModel(undo);
        var manager = new SelectionManager(nodes, panel, undo);

        nodes[0].IsSelected = true;
        Assert.False(manager.AlignLeftCommand.CanExecute(null));

        nodes[1].IsSelected = true;
        Assert.True(manager.AlignLeftCommand.CanExecute(null));
    }

    [Fact]
    public void DistributeCommands_RequireAtLeastThreeSelectedNodes()
    {
        var nodes = new ObservableCollection<NodeViewModel>
        {
            Node("a"),
            Node("b"),
            Node("c")
        };

        var undo = new UndoRedoStack(new CanvasViewModel());
        var panel = new PropertyPanelViewModel(undo);
        var manager = new SelectionManager(nodes, panel, undo);

        nodes[0].IsSelected = true;
        nodes[1].IsSelected = true;
        Assert.False(manager.DistributeHCommand.CanExecute(null));

        nodes[2].IsSelected = true;
        Assert.True(manager.DistributeHCommand.CanExecute(null));
    }
}



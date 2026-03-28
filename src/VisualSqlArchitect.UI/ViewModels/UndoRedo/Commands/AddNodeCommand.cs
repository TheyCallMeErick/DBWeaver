using Avalonia;

namespace VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

public sealed class AddNodeCommand(NodeViewModel node) : ICanvasCommand
{
    private readonly NodeViewModel _node = node;
    public string Description => $"Add {_node.Title}";

    public void Execute(CanvasViewModel canvas)
    {
        if (!canvas.Nodes.Contains(_node))
            canvas.Nodes.Add(_node);
    }

    public void Undo(CanvasViewModel canvas)
    {
        // Also remove any wires connected to this node
        var wires = canvas
            .Connections.Where(c => c.FromPin.Owner == _node || c.ToPin?.Owner == _node)
            .ToList();
        foreach (ConnectionViewModel? w in wires)
            canvas.Connections.Remove(w);
        canvas.Nodes.Remove(_node);
    }
}

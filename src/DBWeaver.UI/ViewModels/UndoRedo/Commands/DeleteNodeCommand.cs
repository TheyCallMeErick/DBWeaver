namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class DeleteNodeCommand(NodeViewModel node) : ICanvasCommand
{
    private readonly NodeViewModel _node = node;
    private readonly List<ConnectionViewModel> _removedConnections = [];
    public string Description => $"Delete {_node.Title}";

    public void Execute(CanvasViewModel canvas)
    {
        _removedConnections.Clear();
        var wires = canvas
            .Connections.Where(c => c.FromPin.Owner == _node || c.ToPin?.Owner == _node)
            .ToList();
        foreach (ConnectionViewModel? w in wires)
        {
            _removedConnections.Add(w);
            canvas.Connections.Remove(w);
        }
        canvas.Nodes.Remove(_node);
    }

    public void Undo(CanvasViewModel canvas)
    {
        canvas.Nodes.Add(_node);
        foreach (ConnectionViewModel w in _removedConnections)
            canvas.Connections.Add(w);
    }
}

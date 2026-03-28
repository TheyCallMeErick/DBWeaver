namespace VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

public sealed class DeleteSelectionCommand : ICanvasCommand
{
    private readonly List<ICanvasCommand> _nestedCommands = [];

    public string Description =>
        _nestedCommands.Count == 1
            ? "Delete 1 node"
            : $"Delete {_nestedCommands.Count} nodes and connections";

    public DeleteSelectionCommand(
        IEnumerable<NodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> wires
    )
    {
        // First delete all wires connected to selected nodes
        foreach (ConnectionViewModel wire in wires)
            _nestedCommands.Add(new DeleteConnectionCommand(wire));

        // Then delete selected nodes
        foreach (NodeViewModel node in nodes)
            _nestedCommands.Add(new DeleteNodeCommand(node));
    }

    public bool HasChanges => _nestedCommands.Count > 0;

    public void Execute(CanvasViewModel canvas)
    {
        foreach (ICanvasCommand cmd in _nestedCommands)
            cmd.Execute(canvas);
    }

    public void Undo(CanvasViewModel canvas)
    {
        foreach (ICanvasCommand? cmd in _nestedCommands.AsEnumerable().Reverse())
            cmd.Undo(canvas);
    }
}

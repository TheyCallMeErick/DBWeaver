namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class DeleteOrphansCommand : ICanvasCommand
{
    private readonly List<ICanvasCommand> _nestedCommands = [];

    public string Description => "Delete orphan nodes";

    public DeleteOrphansCommand(
        IEnumerable<NodeViewModel> orphanNodes,
        IEnumerable<ConnectionViewModel> relatedWires
    )
    {
        foreach (ConnectionViewModel w in relatedWires)
            _nestedCommands.Add(new DeleteConnectionCommand(w));

        foreach (NodeViewModel n in orphanNodes)
            _nestedCommands.Add(new DeleteNodeCommand(n));
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

namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

/// <summary>
/// Groups multiple <see cref="ICanvasCommand"/>s into a single undo / redo entry.
///
/// Used by the transaction mechanism (<see cref="UndoRedoStack.BeginTransaction"/> /
/// <see cref="UndoRedoStack.CommitTransaction"/>) and by multi-node drag to record
/// all node movements as one atomic history entry.
/// </summary>
public sealed class CompositeCommand : ICanvasCommand
{
    private readonly IReadOnlyList<ICanvasCommand> _commands;

    public string Description { get; }

    public CompositeCommand(string description, IEnumerable<ICanvasCommand> commands)
    {
        Description = description;
        _commands = [.. commands];
    }

    public void Execute(CanvasViewModel canvas)
    {
        foreach (ICanvasCommand cmd in _commands)
            cmd.Execute(canvas);
    }

    public void Undo(CanvasViewModel canvas)
    {
        foreach (ICanvasCommand cmd in _commands.Reverse())
            cmd.Undo(canvas);
    }
}

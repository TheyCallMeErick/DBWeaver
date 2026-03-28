namespace VisualSqlArchitect.UI.ViewModels.UndoRedo;

/// <summary>
/// A reversible canvas mutation.
/// All canvas operations (add node, delete node, add wire, move node, edit parameter)
/// implement this so they can be undone / redone.
/// </summary>
public interface ICanvasCommand
{
    string Description { get; }
    void Execute(CanvasViewModel canvas);
    void Undo(CanvasViewModel canvas);
}

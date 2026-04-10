namespace DBWeaver.UI.ViewModels.UndoRedo;

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

    /// <summary>
    /// Attempt to merge <paramref name="next"/> into this command.
    /// Returns a merged <see cref="ICanvasCommand"/> that replaces both, or
    /// <c>null</c> if merging is not applicable.
    ///
    /// Used to coalesce consecutive edits of the same parameter into one
    /// undo entry (e.g. rapid keystrokes in a text field).
    /// </summary>
    ICanvasCommand? TryMerge(ICanvasCommand next) => null;
}

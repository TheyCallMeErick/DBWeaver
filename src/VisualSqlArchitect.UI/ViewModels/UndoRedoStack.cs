using VisualSqlArchitect.UI.ViewModels.UndoRedo;

namespace VisualSqlArchitect.UI.ViewModels;

// ═════════════════════════════════════════════════════════════════════════════
// UNDO / REDO STACK
// ═════════════════════════════════════════════════════════════════════════════

public sealed class UndoRedoStack(CanvasViewModel canvas) : ViewModelBase
{
    // LinkedList gives O(1) push/pop from either end and O(1) oldest-entry trim.
    // Convention: most-recently-executed command is the Last node.
    private readonly LinkedList<ICanvasCommand> _undoStack = new();
    private readonly LinkedList<ICanvasCommand> _redoStack = new();
    private readonly CanvasViewModel _canvas = canvas;
    private const int MaxHistory = 200;

    // ── State ─────────────────────────────────────────────────────────────────

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoDepth => _undoStack.Count;

    public string UndoDescription =>
        _undoStack.Last is not null ? _undoStack.Last.Value.Description : string.Empty;
    public string RedoDescription =>
        _redoStack.Last is not null ? _redoStack.Last.Value.Description : string.Empty;

    public IReadOnlyList<string> UndoHistory => _undoStack.Select(c => c.Description).ToList();

    // ── Operations ────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a command and pushes it onto the undo stack.
    /// Clears the redo stack (branching history collapses).
    /// </summary>
    public void Execute(ICanvasCommand command)
    {
        command.Execute(_canvas);
        _undoStack.AddLast(command);
        _redoStack.Clear();

        // Trim oldest entry — O(1), no array allocation
        if (_undoStack.Count > MaxHistory)
            _undoStack.RemoveFirst();

        Notify();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;
        ICanvasCommand command = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        command.Undo(_canvas);
        _redoStack.AddLast(command);
        Notify();
    }

    public void Redo()
    {
        if (!CanRedo)
            return;
        ICanvasCommand command = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        command.Execute(_canvas);
        _undoStack.AddLast(command);
        Notify();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Notify();
    }

    private void Notify()
    {
        RaisePropertyChanged(nameof(CanUndo));
        RaisePropertyChanged(nameof(CanRedo));
        RaisePropertyChanged(nameof(UndoDescription));
        RaisePropertyChanged(nameof(RedoDescription));
        RaisePropertyChanged(nameof(UndoHistory));
    }
}

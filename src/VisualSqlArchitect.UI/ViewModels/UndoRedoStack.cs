namespace VisualSqlArchitect.UI.ViewModels;

// ═════════════════════════════════════════════════════════════════════════════
// COMMAND INTERFACE
// ═════════════════════════════════════════════════════════════════════════════

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

// ═════════════════════════════════════════════════════════════════════════════
// UNDO / REDO STACK
// ═════════════════════════════════════════════════════════════════════════════

public sealed class UndoRedoStack : ViewModelBase
{
    private readonly Stack<ICanvasCommand> _undoStack = new();
    private readonly Stack<ICanvasCommand> _redoStack = new();
    private readonly CanvasViewModel       _canvas;
    private const int MaxHistory = 200;

    public UndoRedoStack(CanvasViewModel canvas) => _canvas = canvas;

    // ── State ─────────────────────────────────────────────────────────────────

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string UndoDescription => _undoStack.TryPeek(out var cmd) ? cmd.Description : string.Empty;
    public string RedoDescription => _redoStack.TryPeek(out var cmd) ? cmd.Description : string.Empty;

    public IReadOnlyList<string> UndoHistory =>
        _undoStack.Select(c => c.Description).ToList();

    // ── Operations ────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a command and pushes it onto the undo stack.
    /// Clears the redo stack (branching history collapses).
    /// </summary>
    public void Execute(ICanvasCommand command)
    {
        command.Execute(_canvas);
        _undoStack.Push(command);
        _redoStack.Clear();

        // Trim history to avoid unbounded memory
        if (_undoStack.Count > MaxHistory)
        {
            var arr = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var c in arr.Take(MaxHistory).Reverse())
                _undoStack.Push(c);
        }

        Notify();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo(_canvas);
        _redoStack.Push(command);
        Notify();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute(_canvas);
        _undoStack.Push(command);
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

// ═════════════════════════════════════════════════════════════════════════════
// CONCRETE COMMANDS
// ═════════════════════════════════════════════════════════════════════════════

// ── Add Node ─────────────────────────────────────────────────────────────────

public sealed class AddNodeCommand : ICanvasCommand
{
    private readonly NodeViewModel _node;
    public string Description => $"Add {_node.Title}";

    public AddNodeCommand(NodeViewModel node) => _node = node;

    public void Execute(CanvasViewModel canvas)
    {
        if (!canvas.Nodes.Contains(_node))
            canvas.Nodes.Add(_node);
    }

    public void Undo(CanvasViewModel canvas)
    {
        // Also remove any wires connected to this node
        var wires = canvas.Connections
            .Where(c => c.FromPin.Owner == _node || c.ToPin?.Owner == _node)
            .ToList();
        foreach (var w in wires) canvas.Connections.Remove(w);
        canvas.Nodes.Remove(_node);
    }
}

// ── Delete Node ───────────────────────────────────────────────────────────────

public sealed class DeleteNodeCommand : ICanvasCommand
{
    private readonly NodeViewModel              _node;
    private readonly List<ConnectionViewModel>  _removedConnections = [];
    public string Description => $"Delete {_node.Title}";

    public DeleteNodeCommand(NodeViewModel node) => _node = node;

    public void Execute(CanvasViewModel canvas)
    {
        _removedConnections.Clear();
        var wires = canvas.Connections
            .Where(c => c.FromPin.Owner == _node || c.ToPin?.Owner == _node)
            .ToList();
        foreach (var w in wires)
        {
            _removedConnections.Add(w);
            canvas.Connections.Remove(w);
        }
        canvas.Nodes.Remove(_node);
    }

    public void Undo(CanvasViewModel canvas)
    {
        canvas.Nodes.Add(_node);
        foreach (var w in _removedConnections)
            canvas.Connections.Add(w);
    }
}

// ── Add Connection ────────────────────────────────────────────────────────────

public sealed class AddConnectionCommand : ICanvasCommand
{
    private readonly ConnectionViewModel  _connection;
    private ConnectionViewModel?          _displaced;    // previous connection evicted from single-input pin
    public string Description => $"Connect {_connection.FromPin.Name} → {_connection.ToPin?.Name}";

    public AddConnectionCommand(ConnectionViewModel connection,
        ConnectionViewModel? displaced = null)
    {
        _connection = connection;
        _displaced  = displaced;
    }

    public void Execute(CanvasViewModel canvas)
    {
        if (_displaced is not null && canvas.Connections.Contains(_displaced))
            canvas.Connections.Remove(_displaced);

        if (!canvas.Connections.Contains(_connection))
            canvas.Connections.Add(_connection);

        _connection.FromPin.IsConnected = true;
        if (_connection.ToPin is not null)
            _connection.ToPin.IsConnected = true;
    }

    public void Undo(CanvasViewModel canvas)
    {
        canvas.Connections.Remove(_connection);
        _connection.FromPin.IsConnected =
            canvas.Connections.Any(c => c.FromPin == _connection.FromPin);

        if (_connection.ToPin is not null)
        {
            _connection.ToPin.IsConnected =
                canvas.Connections.Any(c => c.ToPin == _connection.ToPin);
        }

        // Restore displaced connection
        if (_displaced is not null)
            canvas.Connections.Add(_displaced);
    }
}

// ── Delete Connection ─────────────────────────────────────────────────────────

public sealed class DeleteConnectionCommand : ICanvasCommand
{
    private readonly ConnectionViewModel _connection;
    public string Description => "Delete connection";

    public DeleteConnectionCommand(ConnectionViewModel connection)
        => _connection = connection;

    public void Execute(CanvasViewModel canvas)
    {
        canvas.Connections.Remove(_connection);
        _connection.FromPin.IsConnected =
            canvas.Connections.Any(c => c.FromPin == _connection.FromPin);
        if (_connection.ToPin is not null)
            _connection.ToPin.IsConnected =
                canvas.Connections.Any(c => c.ToPin == _connection.ToPin);
    }

    public void Undo(CanvasViewModel canvas)
    {
        canvas.Connections.Add(_connection);
        _connection.FromPin.IsConnected = true;
        if (_connection.ToPin is not null)
            _connection.ToPin.IsConnected = true;
    }
}

// ── Move Node ─────────────────────────────────────────────────────────────────

public sealed class MoveNodeCommand : ICanvasCommand
{
    private readonly NodeViewModel _node;
    private readonly Avalonia.Point _from;
    private readonly Avalonia.Point _to;
    public string Description => $"Move {_node.Title}";

    public MoveNodeCommand(NodeViewModel node, Avalonia.Point from, Avalonia.Point to)
    {
        _node = node;
        _from = from;
        _to   = to;
    }

    public void Execute(CanvasViewModel canvas) => _node.Position = _to;
    public void Undo(CanvasViewModel canvas)    => _node.Position = _from;
}

// ── Edit Parameter ────────────────────────────────────────────────────────────

public sealed class EditParameterCommand : ICanvasCommand
{
    private readonly NodeViewModel _node;
    private readonly string        _paramName;
    private readonly string?       _oldValue;
    private readonly string?       _newValue;
    public string Description => $"Edit {_node.Title}.{_paramName}";

    public EditParameterCommand(NodeViewModel node, string paramName,
        string? oldValue, string? newValue)
    {
        _node      = node;
        _paramName = paramName;
        _oldValue  = oldValue;
        _newValue  = newValue;
    }

    public void Execute(CanvasViewModel canvas)
    {
        if (_newValue is null) _node.Parameters.Remove(_paramName);
        else _node.Parameters[_paramName] = _newValue;
        _node.RaiseParameterChanged(_paramName);
    }

    public void Undo(CanvasViewModel canvas)
    {
        if (_oldValue is null) _node.Parameters.Remove(_paramName);
        else _node.Parameters[_paramName] = _oldValue;
        _node.RaiseParameterChanged(_paramName);
    }
}

// ── Multi-delete ──────────────────────────────────────────────────────────────

public sealed class DeleteSelectionCommand : ICanvasCommand
{
    private readonly List<ICanvasCommand> _inner = [];
    public string Description => $"Delete {_inner.Count} items";

    public DeleteSelectionCommand(IEnumerable<NodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> wires)
    {
        foreach (var w in wires) _inner.Add(new DeleteConnectionCommand(w));
        foreach (var n in nodes) _inner.Add(new DeleteNodeCommand(n));
    }

    public void Execute(CanvasViewModel canvas)
    {
        foreach (var cmd in _inner) cmd.Execute(canvas);
    }

    public void Undo(CanvasViewModel canvas)
    {
        // Restore in reverse order
        foreach (var cmd in Enumerable.Reverse(_inner)) cmd.Undo(canvas);
    }
}

using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.ViewModels.UndoRedo;

/// <summary>
/// Command that captures and restores a canvas state.
/// Used when operations like SQL import destroy the canvas and need to provide undo capability.
///
/// Pattern:
/// - The operation (e.g., SQL import) happens BEFORE this command is added to the undo stack
/// - When Execute() is called by the undo stack, it does nothing (operation already done)
/// - When Undo() is called, it restores the pre-operation state
/// - When Redo() is called, it clears the canvas (redo the operation)
/// </summary>
public sealed class RestoreCanvasStateCommand : ICanvasCommand
{
    private readonly List<NodeViewModel> _savedNodes;
    private readonly List<ConnectionViewModel> _savedConnections;
    private List<NodeViewModel>? _afterNodes;
    private List<ConnectionViewModel>? _afterConnections;
    private bool _hasAfterSnapshot;
    private bool _hasBeenRegistered;

    public string Description { get; }

    /// <summary>
    /// Creates a command that preserves canvas state for undo capability after destructive operations.
    /// </summary>
    /// <param name="canvas">Current canvas to snapshot</param>
    /// <param name="operationDescription">Description of what operation can be undone (e.g., "SQL Import")</param>
    public RestoreCanvasStateCommand(CanvasViewModel canvas, string operationDescription = "Canvas Change")
    {
        Description = $"Undo {operationDescription}";

        // Snapshot current state (before the destructive operation)
        _savedNodes = canvas.Nodes.ToList();
        _savedConnections = canvas.Connections.ToList();
    }

    public void Execute(CanvasViewModel canvas)
    {
        // First Execute call happens when command is pushed to undo stack.
        // Operation is already applied by caller, so keep current state unchanged.
        if (!_hasBeenRegistered)
        {
            _hasBeenRegistered = true;
            return;
        }

        // Subsequent Execute calls are Redo — restore post-operation state when available.
        if (_hasAfterSnapshot)
            Restore(canvas, _afterNodes!, _afterConnections!);
    }

    public void Undo(CanvasViewModel canvas)
    {
        // Undo should restore the pre-operation state.
        Restore(canvas, _savedNodes, _savedConnections);
    }

    /// <summary>
    /// Captures the post-operation state so Redo can reapply it after Undo.
    /// </summary>
    public void CaptureAfterState(CanvasViewModel canvas)
    {
        _afterNodes = canvas.Nodes.ToList();
        _afterConnections = canvas.Connections.ToList();
        _hasAfterSnapshot = true;
    }

    private static void Restore(
        CanvasViewModel canvas,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionViewModel> connections)
    {
        canvas.Connections.Clear();
        canvas.Nodes.Clear();

        foreach (NodeViewModel node in nodes)
            canvas.Nodes.Add(node);

        foreach (ConnectionViewModel conn in connections)
            canvas.Connections.Add(conn);
    }
}


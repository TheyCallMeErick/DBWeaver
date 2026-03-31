using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

/// <summary>
/// Applies a new Z-order to a set of nodes so layering operations are undoable.
/// </summary>
public sealed class ReorderNodesCommand : ICanvasCommand
{
    private readonly IReadOnlyDictionary<NodeViewModel, int> _from;
    private readonly IReadOnlyDictionary<NodeViewModel, int> _to;

    public string Description { get; }

    public ReorderNodesCommand(
        string description,
        IReadOnlyDictionary<NodeViewModel, int> from,
        IReadOnlyDictionary<NodeViewModel, int> to
    )
    {
        Description = description;
        _from = from;
        _to = to;
    }

    public void Execute(CanvasViewModel canvas)
    {
        foreach ((NodeViewModel node, int z) in _to)
            node.ZOrder = z;
    }

    public void Undo(CanvasViewModel canvas)
    {
        foreach ((NodeViewModel node, int z) in _from)
            node.ZOrder = z;
    }
}

using Avalonia;

namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class MoveNodeCommand(NodeViewModel node, Point from, Point to) : ICanvasCommand
{
    private readonly NodeViewModel _node = node;
    private readonly Point _from = from;
    private readonly Point _to = to;
    public string Description => $"Move {_node.Title}";

    public void Execute(CanvasViewModel canvas) => _node.Position = _to;

    public void Undo(CanvasViewModel canvas) => _node.Position = _from;
}

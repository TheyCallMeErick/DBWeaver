using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.ViewModels.UndoRedo.Commands;

public sealed class AutoLayoutCommand : ICanvasCommand
{
    private readonly List<(NodeViewModel Node, Point OldPos, Point NewPos)> _moves = [];

    public string Description => $"Auto Layout ({_moves.Count} node(s) repositioned)";

    public AutoLayoutCommand(CanvasViewModel canvas, IReadOnlyList<NodeViewModel>? scope = null)
    {
        var nodes = (scope ?? canvas.Nodes).ToList();
        if (nodes.Count == 0)
            return;

        Dictionary<NodeViewModel, Point> newPositions = NodeLayoutManager.ComputeAutoLayout(
            nodes,
            [.. canvas.Connections],
            IsOutputNode,
            new Point(60, 60)
        );

        foreach (KeyValuePair<NodeViewModel, Point> kvp in newPositions)
            _moves.Add((kvp.Key, kvp.Key.Position, kvp.Value));
    }

    public void Execute(CanvasViewModel canvas)
    {
        foreach ((NodeViewModel node, Point _, Point newPos) in _moves)
            node.Position = newPos;
    }

    public void Undo(CanvasViewModel canvas)
    {
        foreach ((NodeViewModel node, Point oldPos, Point _) in _moves)
            node.Position = oldPos;
    }

    private static bool IsOutputNode(NodeViewModel n) =>
        n.Type
            is NodeType.ResultOutput
                or NodeType.WhereOutput
                or NodeType.SelectOutput
                or NodeType.HtmlExport
                or NodeType.JsonExport
                or NodeType.CsvExport
                or NodeType.ExcelExport
                or NodeType.CreateTableOutput
                or NodeType.AlterTableOutput
                or NodeType.CreateIndexOutput;
}

using Avalonia;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

public sealed class AutoLayoutCommand : ICanvasCommand
{
    private readonly List<(NodeViewModel Node, Point OldPos, Point NewPos)> _moves = [];

    public string Description => $"Auto Layout ({_moves.Count} node(s) repositioned)";

    private const double NodeWidth = 230;
    private const double NodeHeight = 130;
    private const double ColGap = 80;
    private const double RowGap = 40;
    private const double OriginX = 60;
    private const double OriginY = 60;

    public AutoLayoutCommand(CanvasViewModel canvas, IReadOnlyList<NodeViewModel>? scope = null)
    {
        var nodes = (scope ?? canvas.Nodes).ToList();
        if (nodes.Count == 0)
            return;

        Dictionary<NodeViewModel, Point> newPositions = ComputeLayout(
            nodes,
            [.. canvas.Connections]
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

    private static Dictionary<NodeViewModel, Point> ComputeLayout(
        List<NodeViewModel> nodes,
        List<ConnectionViewModel> connections
    )
    {
        var forward = nodes.ToDictionary(n => n, _ => new HashSet<NodeViewModel>());
        var backward = nodes.ToDictionary(n => n, _ => new HashSet<NodeViewModel>());

        foreach (ConnectionViewModel conn in connections)
        {
            NodeViewModel from = conn.FromPin.Owner;
            NodeViewModel? to = conn.ToPin?.Owner;
            if (to is null || !forward.ContainsKey(from) || !forward.ContainsKey(to))
                continue;
            forward[from].Add(to);
            backward[to].Add(from);
        }

        var layer = new Dictionary<NodeViewModel, int>();
        var inDegree = nodes.ToDictionary(n => n, n => backward[n].Count);
        var queue = new Queue<NodeViewModel>(nodes.Where(n => inDegree[n] == 0));

        while (queue.Count > 0)
        {
            NodeViewModel node = queue.Dequeue();
            layer[node] =
                backward[node].Count == 0
                    ? 0
                    : backward[node].Max(p => layer.TryGetValue(p, out int l) ? l : 0) + 1;

            foreach (NodeViewModel? next in forward[node])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        foreach (NodeViewModel? n in nodes.Where(n => !layer.ContainsKey(n)))
            layer[n] = 0;

        int maxLayer = layer.Values.DefaultIfEmpty(0).Max();
        int sinkLayer = maxLayer + 1;
        foreach (NodeViewModel? n in nodes.Where(n => IsOutputNode(n)))
            layer[n] = sinkLayer;

        maxLayer = layer.Values.DefaultIfEmpty(0).Max();

        var byLayer = Enumerable
            .Range(0, maxLayer + 1)
            .ToDictionary(
                l => l,
                l =>
                    nodes
                        .Where(n => layer[n] == l)
                        .OrderBy(n =>
                            backward[n].Count == 0
                                ? n.Position.Y
                                : backward[n].Average(p => p.Position.Y)
                        )
                        .ToList()
            );

        var result = new Dictionary<NodeViewModel, Point>();
        for (int col = 0; col <= maxLayer; col++)
        {
            List<NodeViewModel> nodesInCol = byLayer[col];
            double x = OriginX + col * (NodeWidth + ColGap);

            for (int row = 0; row < nodesInCol.Count; row++)
            {
                double y = OriginY + row * (NodeHeight + RowGap);
                result[nodesInCol[row]] = new Point(x, y);
            }
        }

        return result;
    }

    private static bool IsOutputNode(NodeViewModel n) =>
        n.Type
            is NodeType.ResultOutput
                or NodeType.WhereOutput
                or NodeType.SelectOutput
                or NodeType.HtmlExport
                or NodeType.JsonExport
                or NodeType.CsvExport
                or NodeType.ExcelExport;
}

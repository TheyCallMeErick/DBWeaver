
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationComparisonValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(NodeViewModel resultOutputNode, List<string> errors)
    {
        HashSet<string> predicateNodes = CollectPredicateNodeIds(resultOutputNode);
        if (predicateNodes.Count == 0)
            return;

        foreach (NodeViewModel node in _canvas.Nodes.Where(n => predicateNodes.Contains(n.Id)))
        {
            switch (node.Type)
            {
                case NodeType.Equals:
                case NodeType.NotEquals:
                case NodeType.GreaterThan:
                case NodeType.GreaterOrEqual:
                case NodeType.LessThan:
                case NodeType.LessOrEqual:
                    ValidateRequiredPins(node, errors, "left", "right");
                    break;

                case NodeType.Between:
                case NodeType.NotBetween:
                    ValidateRequiredPins(node, errors, "value", "low", "high");
                    break;

                case NodeType.IsNull:
                case NodeType.IsNotNull:
                    ValidateRequiredPins(node, errors, "value");
                    break;

                case NodeType.Like:
                    ValidateRequiredPins(node, errors, "text");
                    if (!node.Parameters.TryGetValue("pattern", out string? pattern)
                        || string.IsNullOrWhiteSpace(pattern))
                    {
                        errors.Add("LIKE node connected to WHERE/HAVING/QUALIFY has empty pattern parameter.");
                    }
                    break;
            }
        }
    }

    private HashSet<string> CollectPredicateNodeIds(NodeViewModel resultOutputNode)
    {
        HashSet<string> predicateRoots = _canvas.Connections
            .Where(c => c.ToPin?.Owner == resultOutputNode
                && (c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("having", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("qualify", StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.FromPin.Owner.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (predicateRoots.Count == 0)
            return [];

        return CollectUpstreamFrom(predicateRoots);
    }

    private void ValidateRequiredPins(NodeViewModel node, List<string> errors, params string[] pinNames)
    {
        foreach (string pinName in pinNames)
        {
            bool hasConnection = _canvas.Connections.Any(c =>
                c.ToPin?.Owner == node
                && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase)
            );

            bool hasLiteral = node.PinLiterals.TryGetValue(pinName, out string? literal)
                && !string.IsNullOrWhiteSpace(literal);

            if (!hasConnection && !hasLiteral)
            {
                errors.Add($"{node.Title} node connected to WHERE/HAVING/QUALIFY is missing required input '{pinName}'.");
            }
        }
    }

    private HashSet<string> CollectUpstreamFrom(HashSet<string> startNodeIds)
    {
        var visited = new HashSet<string>(startNodeIds, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(startNodeIds);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel connection in _canvas.Connections.Where(c => c.ToPin?.Owner.Id == current))
            {
                string upstream = connection.FromPin.Owner.Id;
                if (visited.Add(upstream))
                    queue.Enqueue(upstream);
            }
        }

        return visited;
    }
}




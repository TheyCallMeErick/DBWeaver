
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationNotAndJsonValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(NodeViewModel resultOutputNode, List<string> errors)
    {
        HashSet<string> predicateNodes = CollectPredicateNodeIds(resultOutputNode);
        HashSet<string> activeSelectNodes = CollectActiveSelectNodeIds(resultOutputNode);
        var activeNodes = new HashSet<string>(predicateNodes, StringComparer.OrdinalIgnoreCase);
        activeNodes.UnionWith(activeSelectNodes);

        if (activeNodes.Count == 0)
            return;

        foreach (NodeViewModel node in _canvas.Nodes.Where(n => activeNodes.Contains(n.Id)))
        {
            if (node.Type == NodeType.Not && predicateNodes.Contains(node.Id))
            {
                ValidateRequiredPinsForContext(node, errors, "WHERE/HAVING/QUALIFY", "condition");
                continue;
            }

            if (node.Type == NodeType.JsonExtract || node.Type == NodeType.JsonValue)
            {
                ValidateRequiredPinsForContext(node, errors, "WHERE/HAVING/QUALIFY/SELECT", "json");

                string path = node.Parameters.TryGetValue("path", out string? pathRaw)
                    ? pathRaw?.Trim() ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(path)
                    || !path.StartsWith("$", StringComparison.Ordinal)
                    || path.Contains(' ', StringComparison.Ordinal))
                {
                    errors.Add("JSON Extract node connected to WHERE/HAVING/QUALIFY/SELECT has invalid 'path'. Use JSONPath like '$.field' or '$[0]'.");
                }

                continue;
            }

            if (node.Type == NodeType.JsonArrayLength)
            {
                ValidateRequiredPinsForContext(node, errors, "WHERE/HAVING/QUALIFY/SELECT", "json");
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

    private HashSet<string> CollectActiveSelectNodeIds(NodeViewModel resultOutputNode)
    {
        var selectRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (PinViewModel pin in QueryCompilationNodeGraphAssembler.CollectProjectedPins(_canvas, resultOutputNode))
            selectRoots.Add(pin.Owner.Id);

        if (selectRoots.Count == 0)
            return [];

        return CollectUpstreamFrom(selectRoots);
    }

    private void ValidateRequiredPinsForContext(
        NodeViewModel node,
        List<string> errors,
        string context,
        params string[] pinNames)
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
                errors.Add($"{node.Title} node connected to {context} is missing required input '{pinName}'.");
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




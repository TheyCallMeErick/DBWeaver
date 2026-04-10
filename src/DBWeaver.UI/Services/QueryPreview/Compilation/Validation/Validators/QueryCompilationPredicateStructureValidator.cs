
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationPredicateStructureValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(NodeViewModel resultOutputNode, List<string> errors)
    {
        HashSet<string> predicateRoots = _canvas.Connections
            .Where(c => c.ToPin?.Owner == resultOutputNode
                && (c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("having", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("qualify", StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.FromPin.Owner.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (predicateRoots.Count == 0)
            return;

        HashSet<string> predicateNodes = CollectUpstreamFrom(predicateRoots);

        foreach (NodeViewModel node in _canvas.Nodes.Where(n => predicateNodes.Contains(n.Id)))
        {
            if (node.Type is NodeType.And or NodeType.Or)
            {
                int inputCount = CountInputsByName(node, "conditions");
                if (inputCount == 0)
                    inputCount = CountInputsByPrefix(node, "cond_");
                if (inputCount == 0)
                {
                    errors.Add($"{node.Title} node connected to WHERE/HAVING/QUALIFY has no conditions; it compiles to a constant expression.");
                    continue;
                }

                if (inputCount == 1)
                {
                    errors.Add($"{node.Title} node connected to WHERE/HAVING/QUALIFY has only one condition; node is redundant.");
                }

                continue;
            }

            if (node.Type == NodeType.CompileWhere)
            {
                int inputCount = CountInputsByName(node, "conditions");
                if (inputCount == 0)
                {
                    errors.Add("COMPILE WHERE node connected to WHERE/HAVING/QUALIFY has no conditions; it compiles to TRUE.");
                    continue;
                }

                if (inputCount == 1)
                {
                    errors.Add("COMPILE WHERE node connected to WHERE/HAVING/QUALIFY has only one condition; node is redundant.");
                }
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

    private int CountInputsByPrefix(NodeViewModel node, string pinPrefix) =>
        _canvas.Connections.Count(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.StartsWith(pinPrefix, StringComparison.OrdinalIgnoreCase)
        );

    private int CountInputsByName(NodeViewModel node, string pinName) =>
        _canvas.Connections.Count(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase)
        );
}




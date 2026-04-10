namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationOutputSourceReachabilityValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(NodeViewModel resultOutputNode, List<string> errors)
    {
        HashSet<string> visitedNodeIds = [];
        var stack = new Stack<NodeViewModel>();
        stack.Push(resultOutputNode);

        while (stack.Count > 0)
        {
            NodeViewModel current = stack.Pop();
            if (!visitedNodeIds.Add(current.Id))
                continue;

            foreach (ConnectionViewModel incoming in _canvas.Connections.Where(c => c.ToPin?.Owner?.Id == current.Id))
            {
                NodeViewModel? upstream = incoming.FromPin?.Owner;
                if (upstream is null)
                    continue;

                stack.Push(upstream);
            }
        }

        bool hasReachableDatasetSource = _canvas.Nodes
            .Where(n => visitedNodeIds.Contains(n.Id))
            .Any(IsDatasetSourceNodeType);

        if (hasReachableDatasetSource)
            return;

        errors.Add("Result Output is not connected to a reachable dataset source.");
    }

    private static bool IsDatasetSourceNodeType(NodeViewModel node)
    {
        return node.Type is
            NodeType.TableSource
            or NodeType.CteSource
            or NodeType.Subquery
            or NodeType.SubqueryReference
            or NodeType.SubqueryDefinition
            or NodeType.RawSqlQuery;
    }
}

namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationSourceConflictValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(
        NodeViewModel resultOutputNode,
        IReadOnlyList<JoinDefinition> joins,
        List<string> errors)
    {
        HashSet<string> upstreamNodeIds = CollectUpstreamNodeIds(resultOutputNode);
        int sourceCount = _canvas.Nodes
            .Where(n => upstreamNodeIds.Contains(n.Id))
            .Count(IsDatasetSourceNode);

        if (sourceCount <= 1)
            return;

        if (joins.Count > 0)
            return;

        errors.Add(
            "Multiple dataset sources are connected to Result Output, but no resolvable JOIN path was found."
        );
    }

    private static bool IsDatasetSourceNode(NodeViewModel node)
    {
        return node.Type is
            NodeType.TableSource
            or NodeType.CteSource
            or NodeType.Subquery
            or NodeType.SubqueryReference
            or NodeType.SubqueryDefinition
            or NodeType.RawSqlQuery;
    }

    private HashSet<string> CollectUpstreamNodeIds(NodeViewModel sinkNode)
    {
        HashSet<string> visited = [sinkNode.Id];
        var queue = new Queue<string>();
        queue.Enqueue(sinkNode.Id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel connection in _canvas.Connections.Where(c => c.ToPin?.Owner.Id == current))
            {
                string fromNodeId = connection.FromPin.Owner.Id;
                if (!visited.Add(fromNodeId))
                    continue;

                queue.Enqueue(fromNodeId);
            }
        }

        return visited;
    }
}

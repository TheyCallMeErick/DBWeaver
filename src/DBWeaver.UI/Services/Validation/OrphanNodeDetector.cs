using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.Validation;

/// <summary>
/// Detects nodes that have no contribution to the final query output
/// (not reachable via backward traversal from ResultOutput / WhereOutput sink nodes).
/// </summary>
public static class OrphanNodeDetector
{
    /// <summary>
    /// Returns the set of node IDs that are orphans — nodes that do not
    /// contribute (directly or transitively) to any output sink.
    /// </summary>
    public static IReadOnlySet<string> DetectOrphanIds(CanvasViewModel canvas)
    {
        if (canvas.Nodes.Count == 0)
            return new HashSet<string>();

        // Identify sink nodes: ResultOutput, WhereOutput, and Export nodes are terminal outputs
        var sinkIds = canvas
            .Nodes.Where(n =>
                n.Type == NodeType.ResultOutput
                || n.Type == NodeType.ReportOutput
                || n.Type == NodeType.WhereOutput
                || n.Type == NodeType.HtmlExport
                || n.Type == NodeType.JsonExport
                || n.Type == NodeType.CsvExport
                || n.Type == NodeType.ExcelExport
            )
            .Select(n => n.Id)
            .ToHashSet();

        // If there are no sinks, every node is potentially orphaned — but returning
        // them all would flood the UI; instead return only truly isolated nodes
        // (nodes with zero connections).
        if (sinkIds.Count == 0)
        {
            var connectedIds = new HashSet<string>(
                canvas.Connections.SelectMany(c =>
                {
                    var ids = new List<string> { c.FromPin.Owner.Id };
                    if (c.ToPin?.Owner is not null)
                        ids.Add(c.ToPin.Owner.Id);
                    return ids;
                })
            );
            return canvas
                .Nodes.Where(n => !connectedIds.Contains(n.Id))
                .Select(n => n.Id)
                .ToHashSet();
        }

        // Build backward adjacency map: nodeId → set of upstream node IDs
        // (i.e. for each connection A→B, map B's id to A's id)
        var upstreamOf = new Dictionary<string, HashSet<string>>();
        foreach (ConnectionViewModel conn in canvas.Connections)
        {
            string? toId = conn.ToPin?.Owner?.Id;
            string? fromId = conn.FromPin?.Owner?.Id;
            if (toId is null || fromId is null)
                continue;
            if (!upstreamOf.TryGetValue(toId, out HashSet<string>? set))
                upstreamOf[toId] = set = [];
            set.Add(fromId);
        }

        // BFS backward from all sinks to mark reachable nodes
        var reachable = new HashSet<string>();
        var queue = new Queue<string>(sinkIds);
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!reachable.Add(id))
                continue;
            if (upstreamOf.TryGetValue(id, out HashSet<string>? upstream))
                foreach (string upId in upstream)
                    if (!reachable.Contains(upId))
                        queue.Enqueue(upId);
        }

        // Orphans = every node NOT reached from a sink, except nodes whose output is optional
        // and that are semantically active in the query graph.
        return canvas.Nodes
            .Where(n => !reachable.Contains(n.Id))
            .Where(n => !IsOptionalOutputNodeWithSemanticContribution(canvas, n))
            .Select(n => n.Id)
            .ToHashSet();
    }

    private static bool IsOptionalOutputNodeWithSemanticContribution(CanvasViewModel canvas, NodeViewModel node)
    {
        if (node.Type != NodeType.Join)
            return false;

        return IsJoinNodeSemanticallyActive(canvas, node);
    }

    private static bool IsJoinNodeSemanticallyActive(CanvasViewModel canvas, NodeViewModel joinNode)
    {
        bool hasConditionInput = canvas.Connections.Any(connection =>
            ReferenceEquals(connection.ToPin?.Owner, joinNode)
            && string.Equals(connection.ToPin?.Name, "condition", StringComparison.OrdinalIgnoreCase));
        if (hasConditionInput)
            return true;

        bool hasLeftInput = canvas.Connections.Any(connection =>
            ReferenceEquals(connection.ToPin?.Owner, joinNode)
            && string.Equals(connection.ToPin?.Name, "left", StringComparison.OrdinalIgnoreCase));
        bool hasRightInput = canvas.Connections.Any(connection =>
            ReferenceEquals(connection.ToPin?.Owner, joinNode)
            && string.Equals(connection.ToPin?.Name, "right", StringComparison.OrdinalIgnoreCase));
        if (hasLeftInput && hasRightInput)
            return true;

        bool hasParameterJoin = joinNode.Parameters.TryGetValue("right_source", out string? rightSource)
            && !string.IsNullOrWhiteSpace(rightSource)
            && joinNode.Parameters.TryGetValue("left_expr", out string? leftExpression)
            && !string.IsNullOrWhiteSpace(leftExpression)
            && joinNode.Parameters.TryGetValue("right_expr", out string? rightExpression)
            && !string.IsNullOrWhiteSpace(rightExpression);

        return hasParameterJoin;
    }
}

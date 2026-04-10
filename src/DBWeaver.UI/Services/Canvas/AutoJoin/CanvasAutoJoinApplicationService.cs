using Avalonia;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.CanvasKit;
using DBWeaver.Metadata;
using DBWeaver.Nodes;

namespace DBWeaver.UI.Services.Canvas.AutoJoin;

internal sealed class CanvasAutoJoinApplicationService : ICanvasAutoJoinApplicationService
{
    public bool TryApplySuggestion(
        JoinSuggestion suggestion,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections,
        Func<NodeDefinition, Point, NodeViewModel> spawnNode,
        Action<PinViewModel, PinViewModel> connectPins)
    {
        NodeViewModel? existingTable = FindTableSourceNode(nodes, suggestion.ExistingTable);
        NodeViewModel? newTable = FindTableSourceNode(nodes, suggestion.NewTable);
        if (existingTable is null || newTable is null)
            return false;

        if (!CanvasAutoJoinSemantics.TryParseQualifiedColumn(suggestion.LeftColumn, out string? leftSource, out string leftColumn)
            || !CanvasAutoJoinSemantics.TryParseQualifiedColumn(suggestion.RightColumn, out string? rightSource, out string rightColumn))
        {
            return false;
        }

        NodeViewModel leftTable = ResolveSourceNode(leftSource, existingTable, newTable, leftColumn);
        NodeViewModel rightTable = ResolveSourceNode(rightSource, newTable, existingTable, rightColumn);

        return TryCreateSimpleJoinNode(
            leftTable,
            leftColumn,
            rightTable,
            rightColumn,
            suggestion.JoinType,
            nodes,
            connections,
            spawnNode,
            connectPins);
    }

    public bool TryCreateManualJoin(
        NodeViewModel leftTable,
        string leftColumn,
        NodeViewModel rightTable,
        string rightColumn,
        string? joinType,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections,
        Func<NodeDefinition, Point, NodeViewModel> spawnNode,
        Action<PinViewModel, PinViewModel> connectPins)
    {
        return TryCreateSimpleJoinNode(
            leftTable,
            leftColumn,
            rightTable,
            rightColumn,
            joinType,
            nodes,
            connections,
            spawnNode,
            connectPins);
    }

    private static bool TryCreateSimpleJoinNode(
        NodeViewModel leftTable,
        string leftColumn,
        NodeViewModel rightTable,
        string rightColumn,
        string? joinType,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections,
        Func<NodeDefinition, Point, NodeViewModel> spawnNode,
        Action<PinViewModel, PinViewModel> connectPins)
    {
        PinViewModel? leftPin = leftTable.OutputPins.FirstOrDefault(p =>
            p.Name.Equals(leftColumn, StringComparison.OrdinalIgnoreCase));
        PinViewModel? rightPin = rightTable.OutputPins.FirstOrDefault(p =>
            p.Name.Equals(rightColumn, StringComparison.OrdinalIgnoreCase));

        if (leftPin is null || rightPin is null)
            return false;

        if (HasJoinBetween(leftPin, rightPin, nodes, connections))
            return false;

        double joinX = Math.Max(leftTable.Position.X, rightTable.Position.X) + 360;
        double joinY = (leftTable.Position.Y + rightTable.Position.Y) / 2.0;
        NodeViewModel joinNode = spawnNode(
            NodeDefinitionRegistry.Get(NodeType.Join),
            new Point(joinX, joinY)
        );

        string normalizedJoinType = string.IsNullOrWhiteSpace(joinType)
            ? "INNER"
            : joinType.Trim().ToUpperInvariant();
        joinNode.Parameters["join_type"] = normalizedJoinType;
        joinNode.Parameters["right_source"] = GetTableIdentifier(rightTable);
        joinNode.Parameters["left_expr"] = $"{GetTableIdentifier(leftTable)}.{leftPin.Name}";
        joinNode.Parameters["right_expr"] = $"{GetTableIdentifier(rightTable)}.{rightPin.Name}";
        joinNode.RaiseParameterChanged("join_type");
        joinNode.RaiseParameterChanged("right_source");
        joinNode.RaiseParameterChanged("left_expr");
        joinNode.RaiseParameterChanged("right_expr");

        PinViewModel? joinLeft = joinNode.InputPins.FirstOrDefault(p => p.Name == "left");
        PinViewModel? joinRight = joinNode.InputPins.FirstOrDefault(p => p.Name == "right");
        if (joinLeft is null || joinRight is null)
            return false;

        connectPins(leftPin, joinLeft);
        connectPins(rightPin, joinRight);
        return true;
    }

    private static NodeViewModel? FindTableSourceNode(IReadOnlyCollection<NodeViewModel> nodes, string tableRef)
    {
        string full = tableRef.Trim();
        string shortName = full.Split('.').Last();

        return nodes.FirstOrDefault(n =>
            n.IsTableSource
            && (
                n.Subtitle.Equals(full, StringComparison.OrdinalIgnoreCase)
                || n.Title.Equals(shortName, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(n.Alias)
                    && n.Alias.Equals(shortName, StringComparison.OrdinalIgnoreCase))
            ));
    }

    private static NodeViewModel ResolveSourceNode(
        string? sourceRef,
        NodeViewModel preferred,
        NodeViewModel fallback,
        string expectedColumn)
    {
        if (!string.IsNullOrWhiteSpace(sourceRef) && MatchesSource(preferred, sourceRef))
            return preferred;

        if (!string.IsNullOrWhiteSpace(sourceRef) && MatchesSource(fallback, sourceRef))
            return fallback;

        if (preferred.OutputPins.Any(p => p.Name.Equals(expectedColumn, StringComparison.OrdinalIgnoreCase)))
            return preferred;

        return fallback;
    }

    private static bool MatchesSource(NodeViewModel node, string sourceRef) =>
        CanvasAutoJoinSemantics.MatchesSource(node.Subtitle, node.Title, node.Alias, sourceRef);

    private static bool HasJoinBetween(
        PinViewModel leftPin,
        PinViewModel rightPin,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections)
    {
        foreach (NodeViewModel node in nodes.Where(n => n.IsJoin))
        {
            ConnectionViewModel? leftConn = connections.FirstOrDefault(c =>
                c.ToPin?.Owner == node && c.ToPin.Name == "left");
            ConnectionViewModel? rightConn = connections.FirstOrDefault(c =>
                c.ToPin?.Owner == node && c.ToPin.Name == "right");

            if (leftConn is null || rightConn is null)
                continue;

            bool sameOrientation = leftConn.FromPin == leftPin && rightConn.FromPin == rightPin;
            bool reversedOrientation = leftConn.FromPin == rightPin && rightConn.FromPin == leftPin;
            if (sameOrientation || reversedOrientation)
                return true;
        }

        return false;
    }

    private static string GetTableIdentifier(NodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.Subtitle))
            return node.Subtitle;

        return node.Title ?? string.Empty;
    }
}






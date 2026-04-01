using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

public sealed class SetOperationHandler
{
    private readonly CanvasViewModel _canvas;

    public SetOperationHandler(CanvasViewModel canvas)
    {
        _canvas = canvas;
    }

    public (SetOperationDefinition? Operation, string? Warning) ResolveSetOperation(NodeViewModel resultOutputNode)
    {
        if (TryResolveSetOperationFromConnectedNode(resultOutputNode, out SetOperationDefinition? fromNode, out string? nodeWarning))
            return (fromNode, nodeWarning);

        if (!resultOutputNode.Parameters.TryGetValue("set_operator", out string? op)
            || string.IsNullOrWhiteSpace(op)
            || op.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        string normalizedOp = op.Trim().ToUpperInvariant();
        if (normalizedOp is not ("UNION" or "UNION ALL" or "INTERSECT" or "EXCEPT"))
        {
            return (
                null,
                $"Set operation '{op.Trim()}' is not supported. Allowed values: UNION, UNION ALL, INTERSECT, EXCEPT."
            );
        }

        if (!resultOutputNode.Parameters.TryGetValue("set_query", out string? query)
            || string.IsNullOrWhiteSpace(query))
        {
            return (
                null,
                $"Set operation '{op.Trim()}' is configured but 'set_query' is empty. Ignoring set operation."
            );
        }

        string rightSql = query.Trim();
        if (!QueryGraphHelpers.LooksLikeSelectStatement(rightSql))
        {
            return (
                null,
                "Set operation 'set_query' must start with SELECT, WITH, or a parenthesized SELECT. Ignoring set operation."
            );
        }

        return (new SetOperationDefinition(normalizedOp, rightSql), null);
    }

    private bool TryResolveSetOperationFromConnectedNode(
        NodeViewModel resultOutputNode,
        out SetOperationDefinition? operation,
        out string? warning)
    {
        operation = null;
        warning = null;

        ConnectionViewModel? wire = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin.Name.Equals("set_operation", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Owner.Type == NodeType.SetOperation
        );

        if (wire?.FromPin?.Owner is not NodeViewModel setNode)
            return false;

        string op = setNode.Parameters.TryGetValue("operator", out string? opRaw)
            ? opRaw ?? "UNION"
            : "UNION";

        string normalizedOp = op.Trim().ToUpperInvariant();
        if (normalizedOp is not ("UNION" or "UNION ALL" or "INTERSECT" or "EXCEPT"))
        {
            warning = $"Set operation '{op.Trim()}' is not supported. Allowed values: UNION, UNION ALL, INTERSECT, EXCEPT.";
            return true;
        }

        string? query = null;
        if (setNode.Parameters.TryGetValue("query", out string? queryRaw)
            && !string.IsNullOrWhiteSpace(queryRaw))
        {
            query = queryRaw;
        }

        if (string.IsNullOrWhiteSpace(query))
            query = QueryGraphHelpers.ResolveTextInput(_canvas, setNode, "query_text");

        if (string.IsNullOrWhiteSpace(query))
        {
            warning = $"Set operation '{op.Trim()}' is configured but query is empty. Ignoring set operation.";
            return true;
        }

        string rightSql = query.Trim();
        if (!QueryGraphHelpers.LooksLikeSelectStatement(rightSql))
        {
            warning = "Set operation query must start with SELECT, WITH, or a parenthesized SELECT. Ignoring set operation.";
            return true;
        }

        operation = new SetOperationDefinition(normalizedOp, rightSql);
        return true;
    }
}

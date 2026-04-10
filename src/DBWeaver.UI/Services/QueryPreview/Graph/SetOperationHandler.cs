
namespace DBWeaver.UI.Services.QueryPreview;

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
        if (TryResolveLegacySetOperationFromResultOutputParameters(resultOutputNode, out SetOperationDefinition? legacy, out string? legacyWarning))
            return (legacy, legacyWarning);
        return (null, null);
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

        string? opFromInput = QueryGraphHelpers.ResolveTextInput(_canvas, setNode, "operator_text");
        string op = !string.IsNullOrWhiteSpace(opFromInput)
            ? opFromInput
            : setNode.Parameters.TryGetValue("operator", out string? opRaw)
                ? opRaw ?? "UNION"
                : "UNION";

        string normalizedOp = op.Trim().ToUpperInvariant();
        if (normalizedOp is not ("UNION" or "UNION ALL" or "INTERSECT" or "EXCEPT"))
        {
            warning = $"Set operation '{op.Trim()}' is not supported. Allowed values: UNION, UNION ALL, INTERSECT, EXCEPT.";
            return true;
        }

        string? query = ResolveStrictTextInput(setNode, "query_text");

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

    private string? ResolveStrictTextInput(NodeViewModel node, string pinName)
    {
        ConnectionViewModel? wire = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase));

        if (wire?.FromPin?.Owner?.Parameters.TryGetValue("value", out string? wireValue) == true
            && !string.IsNullOrWhiteSpace(wireValue))
        {
            return wireValue.Trim();
        }

        if (node.PinLiterals.TryGetValue(pinName, out string? literal)
            && !string.IsNullOrWhiteSpace(literal))
        {
            return literal.Trim().Trim('\'', '"').Trim();
        }

        return null;
    }

    private static bool TryResolveLegacySetOperationFromResultOutputParameters(
        NodeViewModel resultOutputNode,
        out SetOperationDefinition? operation,
        out string? warning)
    {
        operation = null;
        warning = null;

        if (!resultOutputNode.Parameters.TryGetValue("set_operator", out string? opRaw))
            return false;

        string op = opRaw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(op))
            return false;

        string normalizedOp = op.ToUpperInvariant();
        if (normalizedOp is not ("UNION" or "UNION ALL" or "INTERSECT" or "EXCEPT"))
        {
            warning = $"Set operation '{op}' is not supported. Allowed values: UNION, UNION ALL, INTERSECT, EXCEPT.";
            return true;
        }

        resultOutputNode.Parameters.TryGetValue("set_query", out string? queryRaw);
        string rightSql = queryRaw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rightSql))
        {
            warning = $"Set operation '{op}' is configured but set_query is empty. Ignoring set operation.";
            return true;
        }

        if (!QueryGraphHelpers.LooksLikeSelectStatement(rightSql))
        {
            warning = "Set operation query must start with SELECT, WITH, or a parenthesized SELECT. Ignoring set operation.";
            return true;
        }

        operation = new SetOperationDefinition(normalizedOp, rightSql);
        return true;
    }
}

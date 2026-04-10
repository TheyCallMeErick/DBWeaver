
namespace DBWeaver.UI.Services.QueryPreview;

public sealed class SubqueryValidator : IGraphValidator
{
    private readonly CanvasViewModel _canvas;

    public SubqueryValidator(CanvasViewModel canvas)
    {
        _canvas = canvas;
    }

    public void Validate(List<string> errors)
    {
        foreach (NodeViewModel subqueryNode in _canvas.Nodes.Where(n =>
            n.Type is NodeType.SubqueryExists or NodeType.SubqueryIn or NodeType.SubqueryScalar))
        {
            string nodeLabel = subqueryNode.Type switch
            {
                NodeType.SubqueryExists => "EXISTS subquery",
                NodeType.SubqueryIn => "IN subquery",
                NodeType.SubqueryScalar => "Scalar subquery",
                _ => "Subquery",
            };

            string? query = QueryGraphHelpers.ResolveTextInput(_canvas, subqueryNode, "query_text");
            if (string.IsNullOrWhiteSpace(query))
                query = ResolveConnectedSubqueryQuery(subqueryNode);

            if (string.IsNullOrWhiteSpace(query))
            {
                errors.Add($"{nodeLabel} is empty. Defaulting to SELECT 1/NULL fallback.");
                continue;
            }

            string trimmedQuery = query.Trim();
            bool isSelectSubquery = QueryGraphHelpers.LooksLikeSelectStatement(trimmedQuery);
            bool isInLiteralList = subqueryNode.Type == NodeType.SubqueryIn && LooksLikeInLiteralList(trimmedQuery);

            if (!isSelectSubquery && !isInLiteralList)
            {
                errors.Add(
                    $"{nodeLabel} must start with SELECT, WITH, or a parenthesized SELECT. SQL may be invalid."
                );
            }
        }
    }

    private string? ResolveConnectedSubqueryQuery(NodeViewModel node)
    {
        ConnectionViewModel? wire = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.Equals("subquery", StringComparison.OrdinalIgnoreCase));
        if (wire?.FromPin?.Owner is not NodeViewModel source
            || source.Type is not (NodeType.Subquery or NodeType.SubqueryReference))
            return null;

        string? byInput = QueryGraphHelpers.ResolveTextInput(_canvas, source, "query_text");
        if (!string.IsNullOrWhiteSpace(byInput))
            return byInput;

        return null;
    }

    private static bool LooksLikeInLiteralList(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        int depth = 0;
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool hasValue = false;
        int tokenStart = 0;

        for (int i = 0; i < sql.Length; i++)
        {
            char ch = sql[i];

            if (inSingleQuote)
            {
                if (ch == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                if (ch == '\'')
                    inSingleQuote = false;

                continue;
            }

            if (inDoubleQuote)
            {
                if (ch == '"' && i + 1 < sql.Length && sql[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                if (ch == '"')
                    inDoubleQuote = false;

                continue;
            }

            if (ch == '\'')
            {
                inSingleQuote = true;
                continue;
            }

            if (ch == '"')
            {
                inDoubleQuote = true;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')')
            {
                if (depth > 0)
                    depth--;
                continue;
            }

            if (ch == ',' && depth == 0)
            {
                if (string.IsNullOrWhiteSpace(sql[tokenStart..i]))
                    return false;

                hasValue = true;
                tokenStart = i + 1;
            }
        }

        if (inSingleQuote || inDoubleQuote || depth != 0)
            return false;

        if (string.IsNullOrWhiteSpace(sql[tokenStart..]))
            return false;

        return hasValue || !string.IsNullOrWhiteSpace(sql);
    }
}

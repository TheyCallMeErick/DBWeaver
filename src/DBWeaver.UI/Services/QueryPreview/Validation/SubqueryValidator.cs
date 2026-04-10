
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

            if (!QueryGraphHelpers.LooksLikeSelectStatement(query.Trim()))
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
}

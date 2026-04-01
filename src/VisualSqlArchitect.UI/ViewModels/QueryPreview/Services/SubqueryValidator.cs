using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

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
            if (string.IsNullOrWhiteSpace(query)
                && subqueryNode.Parameters.TryGetValue("query", out string? byParam)
                && !string.IsNullOrWhiteSpace(byParam))
            {
                query = byParam;
            }

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
}

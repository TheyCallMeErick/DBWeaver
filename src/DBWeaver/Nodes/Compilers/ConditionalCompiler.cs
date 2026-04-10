using DBWeaver.Expressions;
using DBWeaver.Expressions.Advanced;
using DBWeaver.Nodes.Definitions;
using DBWeaver.Registry;

namespace DBWeaver.Nodes.Compilers;

/// <summary>
/// Compiles conditional/control-flow nodes: CASE, NULL Fill, Value Map.
/// These nodes implement branching logic in SQL expressions.
/// </summary>
public sealed class ConditionalCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType is NodeType.NullFill or NodeType.EmptyFill or NodeType.ValueMap;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.NullFill => CompileNullFill(node, ctx),
            NodeType.EmptyFill => CompileEmptyFill(node, ctx),
            NodeType.ValueMap => CompileValueMap(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileNullFill(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        ISqlExpression fallback = ctx.ResolveInput(node.Id, "fallback");

        // COALESCE(value, fallback)
        return new FunctionCallExpr(SqlFn.Coalesce, [value, fallback], PinDataType.Expression);
    }

    private static ISqlExpression CompileEmptyFill(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        ISqlExpression fallback = ctx.ResolveInput(node.Id, "fallback");

        // CASE WHEN value = '' THEN fallback ELSE value END
        var emptyCheck = new ComparisonExpr(
            value,
            ComparisonOperator.Eq,
            new StringLiteralExpr("")
        );
        return new CaseExpr([new WhenClause(emptyCheck, fallback)], value);
    }

    private static ISqlExpression CompileValueMap(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");

        // Build a CASE with all mapping pairs
        var cases = new List<WhenClause>();

        // Each parameter is a mapping: key=value pairs
        foreach (KeyValuePair<string, string> param in node.Parameters)
        {
            if (param.Key.StartsWith("map_key_"))
            {
                string index = param.Key["map_key_".Length..];
                if (node.Parameters.TryGetValue($"map_value_{index}", out string? mapValue))
                {
                    var condition = new ComparisonExpr(
                        value,
                        ComparisonOperator.Eq,
                        new StringLiteralExpr(param.Value ?? "")
                    );
                    var result = new StringLiteralExpr(mapValue ?? "");
                    cases.Add(new WhenClause(condition, result));
                }
            }
        }

        // Default to the value itself if no match
        return new CaseExpr(cases, value);
    }
}

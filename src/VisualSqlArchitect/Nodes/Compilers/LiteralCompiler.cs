using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Expressions.Literals;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles literal and value nodes: ValueNumber, ValueString, ValueDateTime, ValueBoolean.
/// These nodes represent constant values in the expression tree.
/// </summary>
public sealed class LiteralCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType
            is NodeType.ValueNumber
                or NodeType.ValueString
                or NodeType.ValueDateTime
                or NodeType.ValueBoolean
                or NodeType.ColumnList;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.ValueNumber => CompileValueNumber(node),
            NodeType.ValueString => CompileValueString(node),
            NodeType.ValueDateTime => CompileValueDateTime(node),
            NodeType.ValueBoolean => CompileValueBoolean(node),
            NodeType.ColumnList => CompileColumnList(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileValueNumber(NodeInstance node)
    {
        string value = node.Parameters.TryGetValue("value", out string? v) ? v : "0";
        return new NumberLiteralExpr(double.Parse(value));
    }

    private static ISqlExpression CompileValueString(NodeInstance node)
    {
        string value = node.Parameters.TryGetValue("value", out string? v) ? v : "";
        return new StringLiteralExpr(value ?? "");
    }

    private static ISqlExpression CompileValueDateTime(NodeInstance node)
    {
        string value = node.Parameters.TryGetValue("value", out string? v) ? v : "";
        return new RawSqlExpr($"'{value}'", PinDataType.DateTime);
    }

    private static ISqlExpression CompileValueBoolean(NodeInstance node)
    {
        bool value =
            node.Parameters.TryGetValue("value", out string? v)
            && (v?.ToLower() == "true" || v == "1");
        return new LiteralExpr(value ? "TRUE" : "FALSE", PinDataType.Boolean);
    }

    private static ISqlExpression CompileColumnList(NodeInstance node, INodeCompilationContext ctx)
    {
        IReadOnlyList<ISqlExpression> inputs = ctx.ResolveInputs(node.Id, "columns");
        // This is typically handled at the SELECT level, not as an expression
        // For now, return the first input or NULL
        return inputs.Count > 0 ? inputs[0] : NullExpr.Instance;
    }
}

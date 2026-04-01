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
                or NodeType.SystemDate
                or NodeType.SystemDateTime
                or NodeType.CurrentDate
                or NodeType.CurrentTime
                or NodeType.ColumnList
                or NodeType.ColumnSetBuilder
                or NodeType.ColumnSetMerge;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.ValueNumber => CompileValueNumber(node),
            NodeType.ValueString => CompileValueString(node),
            NodeType.ValueDateTime => CompileValueDateTime(node),
            NodeType.ValueBoolean => CompileValueBoolean(node),
            NodeType.SystemDate => CompileSystemDate(ctx),
            NodeType.SystemDateTime => CompileSystemDate(ctx),
            NodeType.CurrentDate => CompileCurrentDate(ctx),
            NodeType.CurrentTime => CompileCurrentTime(ctx),
            NodeType.ColumnList => CompileColumnList(node, ctx),
            NodeType.ColumnSetBuilder => CompileColumnSetBuilder(node, ctx),
            NodeType.ColumnSetMerge => CompileColumnSetMerge(node, ctx),

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

    private static ISqlExpression CompileColumnSetBuilder(NodeInstance node, INodeCompilationContext ctx)
    {
        IReadOnlyList<ISqlExpression> inputs = ctx.ResolveInputs(node.Id, "columns");
        return inputs.Count > 0 ? inputs[0] : NullExpr.Instance;
    }

    private static ISqlExpression CompileColumnSetMerge(NodeInstance node, INodeCompilationContext ctx)
    {
        IReadOnlyList<ISqlExpression> inputs = ctx.ResolveInputs(node.Id, "sets");
        return inputs.Count > 0 ? inputs[0] : NullExpr.Instance;
    }

    private static ISqlExpression CompileSystemDate(INodeCompilationContext ctx)
    {
        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer => "GETDATE()",
            Core.DatabaseProvider.MySql => "NOW()",
            Core.DatabaseProvider.Postgres => "CURRENT_TIMESTAMP",
            _ => "CURRENT_TIMESTAMP",
        };

        return new RawSqlExpr(sql, PinDataType.DateTime);
    }

    private static ISqlExpression CompileCurrentDate(INodeCompilationContext ctx)
    {
        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer => "CAST(GETDATE() AS DATE)",
            Core.DatabaseProvider.MySql => "CURDATE()",
            Core.DatabaseProvider.Postgres => "CURRENT_DATE",
            _ => "CURRENT_DATE",
        };

        return new RawSqlExpr(sql, PinDataType.DateTime);
    }

    private static ISqlExpression CompileCurrentTime(INodeCompilationContext ctx)
    {
        string sql = ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.SqlServer => "CAST(GETDATE() AS TIME)",
            Core.DatabaseProvider.MySql => "CURTIME()",
            Core.DatabaseProvider.Postgres => "CURRENT_TIME",
            _ => "CURRENT_TIME",
        };

        return new RawSqlExpr(sql, PinDataType.DateTime);
    }
}

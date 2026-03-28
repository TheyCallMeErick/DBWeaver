using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles string transformation nodes: UPPER, LOWER, TRIM, SUBSTRING, REGEX*, REPLACE, CONCAT.
/// These nodes transform text values using SQL functions.
/// </summary>
public sealed class StringTransformCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType
            is NodeType.Upper
                or NodeType.Lower
                or NodeType.Trim
                or NodeType.StringLength
                or NodeType.Concat
                or NodeType.Substring
                or NodeType.RegexMatch
                or NodeType.RegexReplace
                or NodeType.RegexExtract
                or NodeType.Replace;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.Upper => new FunctionCallExpr(
                SqlFn.Upper,
                [ctx.ResolveInput(node.Id, "text")],
                PinDataType.Text
            ),

            NodeType.Lower => new FunctionCallExpr(
                SqlFn.Lower,
                [ctx.ResolveInput(node.Id, "text")],
                PinDataType.Text
            ),

            NodeType.Trim => new FunctionCallExpr(
                SqlFn.Trim,
                [ctx.ResolveInput(node.Id, "text")],
                PinDataType.Text
            ),

            NodeType.StringLength => new FunctionCallExpr(
                SqlFn.Length,
                [ctx.ResolveInput(node.Id, "text")],
                PinDataType.Number
            ),

            NodeType.Concat => CompileConcat(node, ctx),
            NodeType.Substring => CompileSubstring(node, ctx),
            NodeType.RegexMatch => CompileRegexMatch(node, ctx),
            NodeType.RegexReplace => CompileRegexReplace(node, ctx),
            NodeType.RegexExtract => CompileRegexExtract(node, ctx),
            NodeType.Replace => CompileReplace(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileConcat(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression a = ctx.ResolveInput(node.Id, "a");
        ISqlExpression b = ctx.ResolveInput(node.Id, "b");
        ISqlExpression? sep =
            ctx.Graph.NodeMap[node.Id].PinLiterals.TryGetValue("separator", out string? s)
            && !string.IsNullOrEmpty(s)
                ? new StringLiteralExpr(s) as ISqlExpression
                : null;

        if (sep is not null)
            return new FunctionCallExpr(SqlFn.Concat, [a, sep, b], PinDataType.Text);

        return new FunctionCallExpr(SqlFn.Concat, [a, b], PinDataType.Text);
    }

    private ISqlExpression CompileSubstring(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression text = ctx.ResolveInput(node.Id, "text");
        ISqlExpression startExpr = ResolveInputOrParam(node, ctx, "start", "1", PinDataType.Number);
        ISqlExpression lengthExpr = ResolveInputOrParam(
            node,
            ctx,
            "length",
            null,
            PinDataType.Number
        );

        // Provider-specific SUBSTRING dialect
        return ctx.EmitContext.Provider switch
        {
            Core.DatabaseProvider.MySql when lengthExpr is not NullExpr => new RawSqlExpr(
                $"SUBSTRING({text.Emit(ctx.EmitContext)}, {startExpr.Emit(ctx.EmitContext)}, {lengthExpr.Emit(ctx.EmitContext)})",
                PinDataType.Text
            ),

            Core.DatabaseProvider.MySql => new RawSqlExpr(
                $"SUBSTRING({text.Emit(ctx.EmitContext)}, {startExpr.Emit(ctx.EmitContext)})",
                PinDataType.Text
            ),

            Core.DatabaseProvider.SqlServer => new RawSqlExpr(
                $"SUBSTRING({text.Emit(ctx.EmitContext)}, {startExpr.Emit(ctx.EmitContext)}, "
                    + $"{(lengthExpr is NullExpr ? $"LEN({text.Emit(ctx.EmitContext)})" : lengthExpr.Emit(ctx.EmitContext))})",
                PinDataType.Text
            ),

            _ when lengthExpr is not NullExpr => new RawSqlExpr(
                $"SUBSTRING({text.Emit(ctx.EmitContext)} FROM {startExpr.Emit(ctx.EmitContext)} FOR {lengthExpr.Emit(ctx.EmitContext)})",
                PinDataType.Text
            ),

            _ => new RawSqlExpr(
                $"SUBSTRING({text.Emit(ctx.EmitContext)} FROM {startExpr.Emit(ctx.EmitContext)})",
                PinDataType.Text
            ),
        };
    }

    private static ISqlExpression CompileRegexMatch(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression text = ctx.ResolveInput(node.Id, "text");

        _ = node.Parameters.TryGetValue("pattern", out string? p) ? $"'{p}'" : "''";

        return new FunctionCallExpr(SqlFn.Regex, [text], PinDataType.Boolean);
    }

    private static ISqlExpression CompileRegexReplace(
        NodeInstance node,
        INodeCompilationContext ctx
    )
    {
        ISqlExpression text = ctx.ResolveInput(node.Id, "text");
        string pattern = node.Parameters.TryGetValue("pattern", out string? p) ? p : "";
        string replace = node.Parameters.TryGetValue("replacement", out string? r) ? r : "";
        return new RawSqlExpr(
            $"REGEXP_REPLACE({text.Emit(ctx.EmitContext)}, '{pattern}', '{replace}')",
            PinDataType.Text
        );
    }

    private static ISqlExpression CompileRegexExtract(
        NodeInstance node,
        INodeCompilationContext ctx
    )
    {
        ISqlExpression text = ctx.ResolveInput(node.Id, "text");
        string pattern = node.Parameters.TryGetValue("pattern", out string? p) ? p : "";
        string group = node.Parameters.TryGetValue("group", out string? g) ? g : "0";
        return new RawSqlExpr(
            $"REGEXP_SUBSTR({text.Emit(ctx.EmitContext)}, '{pattern}', 1, 1, NULL, {group})",
            PinDataType.Text
        );
    }

    private static ISqlExpression CompileReplace(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression text = ctx.ResolveInput(node.Id, "text");
        string find = node.Parameters.TryGetValue("find", out string? f) ? f : "";
        string repl = node.Parameters.TryGetValue("replacement", out string? r) ? r : "";
        return new FunctionCallExpr(
            SqlFn.Replace,
            [text, new StringLiteralExpr(find), new StringLiteralExpr(repl)],
            PinDataType.Text
        );
    }

    private static ISqlExpression ResolveInputOrParam(
        NodeInstance node,
        INodeCompilationContext ctx,
        string pinName,
        string? paramName,
        PinDataType expectedType
    )
    {
        Connection? wire = ctx.Graph.GetSingleInputConnection(node.Id, pinName);
        if (wire is not null)
            return ctx.Resolve(wire.FromNodeId, wire.FromPinName);

        if (paramName is not null && node.Parameters.TryGetValue(paramName, out string? value))
            return new NumberLiteralExpr(double.Parse(value ?? "0"));

        return NullExpr.Instance;
    }
}

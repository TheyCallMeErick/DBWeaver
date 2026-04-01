using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

public sealed class ComparisonNodeCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType
            is NodeType.Equals
                or NodeType.NotEquals
                or NodeType.GreaterThan
                or NodeType.GreaterOrEqual
                or NodeType.LessThan
                or NodeType.LessOrEqual
                or NodeType.Between
                or NodeType.NotBetween
                or NodeType.IsNull
                or NodeType.IsNotNull
                or NodeType.Like
                or NodeType.NotLike
                or NodeType.SubqueryExists
                or NodeType.SubqueryIn
                or NodeType.SubqueryScalar
                or NodeType.Cast
                or NodeType.ColumnRefCast
                or NodeType.ScalarFromColumn;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.Equals => CompileComparison(node, ctx, ComparisonOperator.Eq),
            NodeType.NotEquals => CompileComparison(node, ctx, ComparisonOperator.Neq),
            NodeType.GreaterThan => CompileComparison(node, ctx, ComparisonOperator.Gt),
            NodeType.GreaterOrEqual => CompileComparison(node, ctx, ComparisonOperator.Gte),
            NodeType.LessThan => CompileComparison(node, ctx, ComparisonOperator.Lt),
            NodeType.LessOrEqual => CompileComparison(node, ctx, ComparisonOperator.Lte),
            NodeType.Between => CompileBetween(node, ctx, negate: false),
            NodeType.NotBetween => CompileBetween(node, ctx, negate: true),
            NodeType.IsNull => new IsNullExpr(ctx.ResolveInput(node.Id, "value")),
            NodeType.IsNotNull => new IsNullExpr(ctx.ResolveInput(node.Id, "value"), Negate: true),
            NodeType.Like => CompileLike(node, ctx, negate: false),
            NodeType.NotLike => CompileLike(node, ctx, negate: true),
            NodeType.SubqueryExists => CompileSubqueryExists(node, ctx),
            NodeType.SubqueryIn => CompileSubqueryIn(node, ctx),
            NodeType.SubqueryScalar => CompileSubqueryScalar(node, ctx),
            NodeType.Cast => CompileCast(node, ctx),
            NodeType.ColumnRefCast => CompileCast(node, ctx),
            NodeType.ScalarFromColumn => CompileScalarFromColumn(node, ctx),
            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileComparison(
        NodeInstance node,
        INodeCompilationContext ctx,
        ComparisonOperator op
    )
    {
        ISqlExpression left = ctx.ResolveInput(node.Id, "left");
        ISqlExpression right = ctx.ResolveInput(node.Id, "right");
        return new ComparisonExpr(left, op, right);
    }

    private static ISqlExpression CompileBetween(
        NodeInstance node,
        INodeCompilationContext ctx,
        bool negate
    )
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        ISqlExpression lower = ctx.ResolveInput(node.Id, "low");
        ISqlExpression upper = ctx.ResolveInput(node.Id, "high");
        return new BetweenExpr(value, lower, upper, negate);
    }

    private static ISqlExpression CompileLike(
        NodeInstance node,
        INodeCompilationContext ctx,
        bool negate
    )
    {
        ISqlExpression text = ctx.ResolveInput(node.Id, "text");
        StringLiteralExpr pattern = node.Parameters.TryGetValue("pattern", out string? p)
            ? new StringLiteralExpr(p ?? "")
            : new StringLiteralExpr("");

        return new ComparisonExpr(
            text,
            negate ? ComparisonOperator.NotLike : ComparisonOperator.Like,
            pattern
        );
    }

    private static ISqlExpression CompileCast(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        string targetType = node.Parameters.TryGetValue("targetType", out string? t) ? t : "TEXT";

        CastTargetType sqlType = targetType.ToUpper() switch
        {
            "INT" or "INTEGER" => CastTargetType.Integer,
            "FLOAT" or "DOUBLE" => CastTargetType.Float,
            "DECIMAL" => CastTargetType.Decimal,
            "DATE" => CastTargetType.Date,
            "DATETIME" => CastTargetType.DateTime,
            "BOOL" or "BOOLEAN" => CastTargetType.Boolean,
            _ => CastTargetType.Text,
        };

        return new CastExpr(value, sqlType);
    }

    private static ISqlExpression CompileScalarFromColumn(
        NodeInstance node,
        INodeCompilationContext ctx
    ) => ctx.ResolveInput(node.Id, "value", PinDataType.ColumnRef);

    private static ISqlExpression CompileSubqueryExists(NodeInstance node, INodeCompilationContext ctx)
    {
        string query = ResolveTextInput(node, "query_text", ctx)
            ?? (node.Parameters.TryGetValue("query", out string? p) ? p ?? string.Empty : string.Empty);

        bool negate =
            node.Parameters.TryGetValue("negate", out string? raw)
            && bool.TryParse(raw, out bool parsed)
            && parsed;

        return new ExistsExpr(query, negate);
    }

    private static ISqlExpression CompileSubqueryIn(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");

        string query = ResolveTextInput(node, "query_text", ctx)
            ?? (node.Parameters.TryGetValue("query", out string? p) ? p ?? string.Empty : string.Empty);

        bool negate =
            node.Parameters.TryGetValue("negate", out string? raw)
            && bool.TryParse(raw, out bool parsed)
            && parsed;

        return new InSubqueryExpr(value, query, negate);
    }

    private static ISqlExpression CompileSubqueryScalar(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression left = ctx.ResolveInput(node.Id, "left");

        string query = ResolveTextInput(node, "query_text", ctx)
            ?? (node.Parameters.TryGetValue("query", out string? p) ? p ?? string.Empty : string.Empty);

        string configuredOp = node.Parameters.TryGetValue("operator", out string? rawOp)
            ? rawOp ?? "="
            : "=";

        ComparisonOperator op = configuredOp.Trim() switch
        {
            "=" => ComparisonOperator.Eq,
            "<>" => ComparisonOperator.Neq,
            ">" => ComparisonOperator.Gt,
            ">=" => ComparisonOperator.Gte,
            "<" => ComparisonOperator.Lt,
            "<=" => ComparisonOperator.Lte,
            _ => ComparisonOperator.Eq,
        };

        return new ComparisonExpr(left, op, new ScalarSubqueryExpr(query));
    }

    private static string? ResolveTextInput(NodeInstance node, string pinName, INodeCompilationContext ctx)
    {
        Connection? wire = ctx.Graph.GetSingleInputConnection(node.Id, pinName);
        bool hasLiteral = node.PinLiterals.ContainsKey(pinName);
        if (wire is null && !hasLiteral)
            return null;

        ISqlExpression expr = ctx.ResolveInput(node.Id, pinName, PinDataType.Text);
        return expr switch
        {
            StringLiteralExpr s when !string.IsNullOrWhiteSpace(s.Value) => s.Value.Trim(),
            LiteralExpr l when !string.IsNullOrWhiteSpace(l.RawValue)
                => l.RawValue.Trim().Trim('\'', '"').Trim(),
            _ => null,
        };
    }
}

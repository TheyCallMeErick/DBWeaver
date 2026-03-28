using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles comparison nodes: =, &lt;&gt;, &gt;, &lt;, BETWEEN, LIKE, IS NULL.
/// These nodes produce boolean expressions used in WHERE clauses.
/// </summary>
public sealed class ComparisonCompiler : INodeCompiler
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
                or NodeType.Cast;

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
            NodeType.Cast => CompileCast(node, ctx),

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

        // Map targetType string to SqlDataType
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
}

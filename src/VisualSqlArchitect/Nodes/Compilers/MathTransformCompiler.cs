using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles mathematical transformation nodes: ROUND, ABS, CEIL, FLOOR, and arithmetic operations.
/// These nodes transform numeric values using SQL math functions.
/// </summary>
public sealed class MathTransformCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType
            is NodeType.Round
                or NodeType.Abs
                or NodeType.Ceil
                or NodeType.Floor
                or NodeType.Add
                or NodeType.Subtract
                or NodeType.Multiply
                or NodeType.Divide;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.Round => CompileRound(node, ctx),
            NodeType.Abs => CompileSimpleMath(node, ctx, "ABS"),
            NodeType.Ceil => CompileCeilFloor(node, ctx, ceil: true),
            NodeType.Floor => CompileCeilFloor(node, ctx, ceil: false),
            NodeType.Add => CompileArithmetic(node, ctx, "+"),
            NodeType.Subtract => CompileArithmetic(node, ctx, "-"),
            NodeType.Multiply => CompileArithmetic(node, ctx, "*"),
            NodeType.Divide => CompileArithmetic(node, ctx, "/"),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileRound(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        NumberLiteralExpr decimals = node.Parameters.TryGetValue("decimals", out string? d)
            ? new NumberLiteralExpr(int.Parse(d ?? "0"))
            : new NumberLiteralExpr(0);

        return new FunctionCallExpr("ROUND", [value, decimals], PinDataType.Number);
    }

    private static ISqlExpression CompileSimpleMath(
        NodeInstance node,
        INodeCompilationContext ctx,
        string functionName
    )
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        return new FunctionCallExpr(functionName, [value], PinDataType.Number);
    }

    private static ISqlExpression CompileCeilFloor(
        NodeInstance node,
        INodeCompilationContext ctx,
        bool ceil
    )
    {
        ISqlExpression value = ctx.ResolveInput(node.Id, "value");
        string funcName = ceil ? "CEIL" : "FLOOR";
        return new FunctionCallExpr(funcName, [value], PinDataType.Number);
    }

    private static ISqlExpression CompileArithmetic(
        NodeInstance node,
        INodeCompilationContext ctx,
        string op
    )
    {
        ISqlExpression left = ctx.ResolveInput(node.Id, "left");
        ISqlExpression right = ctx.ResolveInput(node.Id, "right");

        // Use RawSqlExpr for infix operators to maintain precedence
        return new RawSqlExpr(
            $"({left.Emit(ctx.EmitContext)} {op} {right.Emit(ctx.EmitContext)})",
            PinDataType.Number
        );
    }
}

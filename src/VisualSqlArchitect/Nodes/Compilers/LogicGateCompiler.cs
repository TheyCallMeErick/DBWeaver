using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Expressions.Operations;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles logical gate nodes: AND, OR, NOT.
/// These nodes combine boolean expressions using logical operators.
/// </summary>
public sealed class LogicGateCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType is NodeType.And or NodeType.Or or NodeType.Not;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.And => CompileLogicGate(node, ctx, LogicOperator.And),
            NodeType.Or => CompileLogicGate(node, ctx, LogicOperator.Or),
            NodeType.Not => new NotExpr(ctx.ResolveInput(node.Id, "condition")),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileLogicGate(
        NodeInstance node,
        INodeCompilationContext ctx,
        LogicOperator op
    )
    {
        IReadOnlyList<ISqlExpression> inputs = ctx.ResolveInputs(node.Id, "inputs");
        if (inputs.Count == 0)
            return new LiteralExpr(op == LogicOperator.And ? "TRUE" : "FALSE", PinDataType.Boolean);

        if (inputs.Count == 1)
            return inputs[0];

        // Use variadic LogicGateExpr
        return new LogicGateExpr(op, inputs);
    }
}

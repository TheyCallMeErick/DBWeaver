using DBWeaver.Expressions;
using DBWeaver.Expressions.Operations;
using DBWeaver.Nodes.Definitions;

namespace DBWeaver.Nodes.Compilers;

/// <summary>
/// Compiles output and modifier nodes: ResultOutput, Top, CompileWhere.
/// These nodes are terminal nodes that don't produce expressions but control query assembly.
/// </summary>
public sealed class OutputCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType is NodeType.ResultOutput
            or NodeType.SelectOutput
            or NodeType.ReportOutput
            or NodeType.Top
            or NodeType.CompileWhere
            or NodeType.SetOperation
            or NodeType.WhereOutput;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.ResultOutput => NullExpr.Instance, // Terminal node
            NodeType.SelectOutput => NullExpr.Instance, // Legacy terminal node
            NodeType.ReportOutput => NullExpr.Instance, // Terminal report sink
            NodeType.Top => NullExpr.Instance, // Modifier node
            NodeType.CompileWhere => CompileWhere(node, ctx),
            NodeType.SetOperation => NullExpr.Instance, // Query combiner metadata node
            NodeType.WhereOutput => CompileWhereOutput(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileWhereOutput(NodeInstance node, INodeCompilationContext ctx)
    {
        ISqlExpression condition = ctx.ResolveInput(node.Id, "condition", PinDataType.Boolean);
        return condition is NullExpr ? new LiteralExpr("1 = 1", PinDataType.Boolean) : condition;
    }

    private static ISqlExpression CompileWhere(NodeInstance node, INodeCompilationContext ctx)
    {
        IReadOnlyList<ISqlExpression> conditions = ResolveCompileWhereConditions(node, ctx);

        if (conditions.Count == 0)
            return new LiteralExpr("1 = 1", PinDataType.Boolean);

        if (conditions.Count == 1)
            return conditions[0];

        return new LogicGateExpr(LogicOperator.And, conditions);
    }

    private static IReadOnlyList<ISqlExpression> ResolveCompileWhereConditions(
        NodeInstance node,
        INodeCompilationContext ctx
    )
    {
        IReadOnlyList<ISqlExpression> directConditions = ctx.ResolveInputs(node.Id, "conditions");
        if (directConditions.Count > 0)
            return directConditions;

        IReadOnlyList<Connection> dynamicConditions = ctx.Graph
            .GetInputConnections(node.Id, "conditions")
            .Concat(
                ctx.Graph.Connections
                    .Where(c =>
                        c.ToNodeId == node.Id
                        && c.ToPinName.StartsWith("cond_", StringComparison.OrdinalIgnoreCase)
                    )
            )
            .Distinct()
            .OrderBy(c => c.ToPinName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dynamicConditions.Count == 0)
            return [];

        return dynamicConditions.Select(c => ctx.Resolve(c.FromNodeId, c.FromPinName)).ToList();
    }
}

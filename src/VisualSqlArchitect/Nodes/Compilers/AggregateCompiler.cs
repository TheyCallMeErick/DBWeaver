using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles aggregate function nodes: COUNT, SUM, AVG, MIN, MAX.
/// These nodes compute single values from multiple rows in a result set.
/// </summary>
public sealed class AggregateCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType
            is NodeType.CountStar
                or NodeType.Sum
                or NodeType.Avg
                or NodeType.Min
                or NodeType.Max;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.CountStar => new AggregateExpr(AggregateFunction.Count, null),
            NodeType.Sum => new AggregateExpr(
                AggregateFunction.Sum,
                ctx.ResolveInput(node.Id, "value")
            ),
            NodeType.Avg => new AggregateExpr(
                AggregateFunction.Avg,
                ctx.ResolveInput(node.Id, "value")
            ),
            NodeType.Min => new AggregateExpr(
                AggregateFunction.Min,
                ctx.ResolveInput(node.Id, "value")
            ),
            NodeType.Max => new AggregateExpr(
                AggregateFunction.Max,
                ctx.ResolveInput(node.Id, "value")
            ),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }
}

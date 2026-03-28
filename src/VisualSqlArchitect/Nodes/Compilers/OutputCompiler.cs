using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Compiles output and modifier nodes: ResultOutput, Top, CompileWhere.
/// These nodes are terminal nodes that don't produce expressions but control query assembly.
/// </summary>
public sealed class OutputCompiler : INodeCompiler
{
    public bool CanCompile(NodeType nodeType) =>
        nodeType is NodeType.ResultOutput or NodeType.Top or NodeType.CompileWhere;

    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        return node.Type switch
        {
            NodeType.ResultOutput => NullExpr.Instance, // Terminal node
            NodeType.Top => NullExpr.Instance, // Modifier node
            NodeType.CompileWhere => CompileWhere(node, ctx),

            _ => throw new NotSupportedException($"Cannot compile {node.Type}"),
        };
    }

    private static ISqlExpression CompileWhere(NodeInstance node, INodeCompilationContext ctx) =>
        // CompileWhere is a directive node that tells the compiler to use a specific input as WHERE
        ctx.ResolveInput(node.Id, "condition");
}

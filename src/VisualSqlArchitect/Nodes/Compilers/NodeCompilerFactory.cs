using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Nodes.Definitions;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Factory and dispatcher for all node compilers.
/// Uses the Strategy pattern to select the appropriate compiler for each node type.
/// </summary>
public sealed class NodeCompilerFactory
{
    private readonly INodeCompiler[] _compilers;

    public NodeCompilerFactory()
    {
        _compilers =
        [
            new DataSourceCompiler(),
            new StringTransformCompiler(),
            new MathTransformCompiler(),
            new ComparisonCompiler(),
            new AggregateCompiler(),
            new ConditionalCompiler(),
            new JsonCompiler(),
            new LogicGateCompiler(),
            new LiteralCompiler(),
            new OutputCompiler(),
        ];
    }

    /// <summary>
    /// Compiles a node by finding the appropriate compiler and delegating to it.
    /// </summary>
    public ISqlExpression Compile(NodeInstance node, INodeCompilationContext ctx, string pinName)
    {
        INodeCompiler compiler = FindCompiler(node.Type);
        return compiler.Compile(node, ctx, pinName);
    }

    /// <summary>
    /// Finds the compiler capable of handling the given node type.
    /// </summary>
    private INodeCompiler FindCompiler(NodeType nodeType)
    {
        INodeCompiler? compiler =
            _compilers.FirstOrDefault(c => c.CanCompile(nodeType))
            ?? throw new NotSupportedException(
                $"No compiler found for NodeType.{nodeType}. "
                    + $"Ensure all node types are covered by at least one compiler."
            );
        return compiler;
    }
}

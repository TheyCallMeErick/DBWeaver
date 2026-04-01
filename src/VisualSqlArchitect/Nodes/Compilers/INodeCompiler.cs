using VisualSqlArchitect.Expressions;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.Nodes.Compilers;

/// <summary>
/// Strategy interface for compiling a specific category of nodes.
/// Each implementation handles compilation of semantically-related node types.
/// </summary>
public interface INodeCompiler
{
    /// <summary>
    /// Returns true if this compiler can handle the given node type.
    /// </summary>
    bool CanCompile(NodeType nodeType);

    /// <summary>
    /// Compiles the given node into an ISqlExpression tree.
    /// </summary>
    /// <param name="node">The node to compile</param>
    /// <param name="context">The compilation context containing the compiler and graph</param>
    /// <param name="pinName">The output pin name (usually "result")</param>
    /// <returns>Compiled SQL expression</returns>
    ISqlExpression Compile(
        NodeInstance node,
        INodeCompilationContext context,
        string pinName = "result"
    );
}

/// <summary>
/// Context passed to compilers during node resolution.
/// Allows compilers to resolve dependencies recursively.
/// </summary>
public interface INodeCompilationContext
{
    /// <summary>
    /// Resolves the expression for a given node and pin.
    /// </summary>
    ISqlExpression Resolve(string nodeId, string pinName = "result");

    /// <summary>
    /// Resolves an input pin (wired or literal).
    /// </summary>
    ISqlExpression ResolveInput(
        string nodeId,
        string pinName,
        PinDataType expectedType = PinDataType.Expression
    );

    /// <summary>
    /// Resolves multi-input pins (for gates with variadic inputs).
    /// </summary>
    IReadOnlyList<ISqlExpression> ResolveInputs(string nodeId, string pinName);

    /// <summary>
    /// Access to the graph being compiled.
    /// </summary>
    NodeGraph Graph { get; }

    /// <summary>
    /// Access to the emit context (provider, functions).
    /// </summary>
    EmitContext EmitContext { get; }
}

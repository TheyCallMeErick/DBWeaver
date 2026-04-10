namespace DBWeaver.Compilation;

/// <summary>
/// Defines the contract for compiling a graph structure into a typed output representation.
/// </summary>
/// <typeparam name="TOutput">The compiled output type (e.g., DDL statements, SQL structures).</typeparam>
public interface IGraphCompiler<TOutput>
{
    /// <summary>
    /// Attempts to compile the given graph. Returns false and populates <paramref name="errors"/>
    /// if the graph is invalid or compilation fails.
    /// </summary>
    /// <param name="graph">The graph to compile.</param>
    /// <param name="output">The compiled output if successful; otherwise default.</param>
    /// <param name="errors">Validation and compilation errors if unsuccessful.</param>
    /// <returns>True if compilation succeeded; otherwise false.</returns>
    bool TryCompile(
        Nodes.NodeGraph graph,
        out TOutput? output,
        out IReadOnlyList<string> errors);
}

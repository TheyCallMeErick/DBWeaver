
namespace DBWeaver.UI.Services.QueryPreview;

/// <summary>
/// Stub compiler for Query mode. Currently does not support full compilation through the interface.
/// Query compilation requires a CanvasViewModel context, which is maintained in QueryGraphBuilder directly.
/// </summary>
public sealed class QueryGraphCompiler : IGraphCompiler<NodeGraph>
{
    private readonly DatabaseProvider _provider;

    public QueryGraphCompiler(DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        _provider = provider;
    }

    /// <summary>
    /// Query compilation through IGraphCompiler<NodeGraph> is not yet implemented.
    /// Use QueryGraphBuilder.TryBuildGraphSnapshot() directly with a CanvasViewModel instead.
    /// </summary>
    public bool TryCompile(
        NodeGraph graph,
        out NodeGraph? output,
        out IReadOnlyList<string> errors)
    {
        output = null;
        errors = ["Query compilation through IGraphCompiler<NodeGraph> requires a CanvasViewModel context. Use QueryGraphBuilder.TryBuildGraphSnapshot() directly instead."];
        return false;
    }
}




namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationExecutionPlanStageFactory(
    CanvasViewModel canvas,
    DatabaseProvider provider,
    Func<NodeViewModel, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, bool, NodeGraph> buildNodeGraph)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;
    private readonly Func<NodeViewModel, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, bool, NodeGraph> _buildNodeGraph = buildNodeGraph;

    public QueryCompilationExecutionPlanStage Create() =>
        new(
            _buildNodeGraph,
            BuildJoins,
            ResolveSetOperation,
            ValidateCtes,
            ValidateSubqueries);

    private (List<JoinDefinition> Joins, List<string> Warnings) BuildJoins(IReadOnlyList<NodeViewModel> sourceNodes)
    {
        var joinResolver = new JoinResolver(_canvas, _provider);
        return joinResolver.BuildJoins(sourceNodes.ToList());
    }

    private (SetOperationDefinition? SetOperation, string? Warning) ResolveSetOperation(NodeViewModel resultOutputNode)
    {
        var setOpHandler = new SetOperationHandler(_canvas);
        (SetOperationDefinition? operation, string? warning) = setOpHandler.ResolveSetOperation(resultOutputNode);
        if (operation is not null || string.IsNullOrWhiteSpace(warning))
            return (operation, warning);

        if (!warning.Contains("query is empty", StringComparison.OrdinalIgnoreCase))
            return (operation, warning);

        return TryResolveLegacyConnectedSetOperation(resultOutputNode) ?? (operation, warning);
    }

    private (SetOperationDefinition? SetOperation, string? Warning)? TryResolveLegacyConnectedSetOperation(NodeViewModel resultOutputNode)
    {
        ConnectionViewModel? wire = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin.Name.Equals("set_operation", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Owner.Type == NodeType.SetOperation);
        if (wire?.FromPin?.Owner is not NodeViewModel setNode)
            return null;

        if (!setNode.Parameters.TryGetValue("query", out string? queryRaw) || string.IsNullOrWhiteSpace(queryRaw))
            return null;

        string opRaw = setNode.Parameters.TryGetValue("operator", out string? opValue) && !string.IsNullOrWhiteSpace(opValue)
            ? opValue!
            : "UNION";
        string normalizedOp = opRaw.Trim().ToUpperInvariant();
        if (normalizedOp is not ("UNION" or "UNION ALL" or "INTERSECT" or "EXCEPT"))
            return null;

        string rightSql = queryRaw.Trim();
        if (!QueryGraphHelpers.LooksLikeSelectStatement(rightSql))
            return null;

        return (new SetOperationDefinition(normalizedOp, rightSql), null);
    }

    private void ValidateCtes(NodeGraph graph, IReadOnlyDictionary<string, string> cteMap, List<string> errors)
    {
        var cteValidator = new CteValidator(_canvas, cteMap, graph.Ctes);
        cteValidator.Validate(errors);
    }

    private void ValidateSubqueries(List<string> errors)
    {
        var subqueryValidator = new SubqueryValidator(_canvas);
        subqueryValidator.Validate(errors);
    }
}



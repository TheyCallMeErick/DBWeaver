
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationExecutionPlanStage(
    Func<NodeViewModel, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, bool, NodeGraph> buildNodeGraph,
    Func<IReadOnlyList<NodeViewModel>, (List<JoinDefinition> Joins, List<string> Warnings)> buildJoins,
    Func<NodeViewModel, (SetOperationDefinition? SetOperation, string? Warning)> resolveSetOperation,
    Action<NodeGraph, IReadOnlyDictionary<string, string>, List<string>> validateCtes,
    Action<List<string>> validateSubqueries)
{
    private readonly Func<NodeViewModel, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, bool, NodeGraph> _buildNodeGraph = buildNodeGraph;
    private readonly Func<IReadOnlyList<NodeViewModel>, (List<JoinDefinition> Joins, List<string> Warnings)> _buildJoins = buildJoins;
    private readonly Func<NodeViewModel, (SetOperationDefinition? SetOperation, string? Warning)> _resolveSetOperation = resolveSetOperation;
    private readonly Action<NodeGraph, IReadOnlyDictionary<string, string>, List<string>> _validateCtes = validateCtes;
    private readonly Action<List<string>> _validateSubqueries = validateSubqueries;

    public QueryCompilationExecutionPlanStageResult Execute(QueryCompilationExecutionPlanStageInput input)
    {
        NodeGraph graph = _buildNodeGraph(
            input.ResultOutputNode,
            input.CteDefinitions,
            input.CteDefinitionNamesById,
            true);

        (List<JoinDefinition> joins, List<string> joinWarnings) = _buildJoins(input.TableNodes);
        input.Errors.AddRange(joinWarnings);

        (SetOperationDefinition? setOperation, string? setOperationWarning) = _resolveSetOperation(input.ResultOutputNode);
        if (!string.IsNullOrWhiteSpace(setOperationWarning))
            input.Errors.Add(setOperationWarning);

        _validateCtes(graph, input.CteDefinitionNamesById, input.Errors);
        _validateSubqueries(input.Errors);

        return new QueryCompilationExecutionPlanStageResult(
            graph,
            joins,
            setOperation,
            input.Errors);
    }
}


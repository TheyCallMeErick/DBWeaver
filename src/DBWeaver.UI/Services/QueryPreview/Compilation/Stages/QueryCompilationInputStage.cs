
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationInputStage(
    Func<IReadOnlyList<NodeViewModel>, Dictionary<string, string>> buildCteDefinitionNameMap,
    Func<IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, (string FromTable, string? Warning)> resolveFromTable,
    Func<PinViewModel, bool> isWildcardProjectionPin,
    Func<string?, bool> isProjectionInputPinName) : IQueryCompilationStage<QueryCompilationInputStageResult>
{
    private readonly Func<IReadOnlyList<NodeViewModel>, Dictionary<string, string>> _buildCteDefinitionNameMap = buildCteDefinitionNameMap;
    private readonly Func<IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, (string FromTable, string? Warning)> _resolveFromTable = resolveFromTable;
    private readonly Func<PinViewModel, bool> _isWildcardProjectionPin = isWildcardProjectionPin;
    private readonly Func<string?, bool> _isProjectionInputPinName = isProjectionInputPinName;

    public QueryCompilationInputStageResult Execute(QueryCompilationPipelineContext context)
    {
        var errors = new List<string>();
        CanvasViewModel canvas = context.Canvas;

        List<NodeViewModel> tableNodes = canvas.Nodes.Where(n => n.Type == NodeType.TableSource).ToList();
        List<NodeViewModel> cteSourceNodes = canvas.Nodes.Where(n => n.Type == NodeType.CteSource).ToList();
        List<NodeViewModel> subqueryNodes = canvas.Nodes
            .Where(n => n.Type is NodeType.Subquery or NodeType.SubqueryReference)
            .ToList();

        if (tableNodes.Count == 0 && cteSourceNodes.Count == 0 && subqueryNodes.Count == 0)
        {
            return new QueryCompilationInputStageResult(
                Snapshot: null,
                ShouldShortCircuit: true,
                ShortCircuitSql: "-- Add a table, CTE source, or Subquery node to start building your query",
                Errors: errors);
        }

        IReadOnlyList<NodeViewModel> topLevelOutputs = ResolveTopLevelResultOutputNodes(canvas);
        if (topLevelOutputs.Count == 0)
        {
            return new QueryCompilationInputStageResult(
                Snapshot: null,
                ShouldShortCircuit: true,
                ShortCircuitSql: "-- Add a Result Output node to generate SQL",
                Errors: errors);
        }
        if (topLevelOutputs.Count > 1)
        {
            return new QueryCompilationInputStageResult(
                Snapshot: null,
                ShouldShortCircuit: true,
                ShortCircuitSql: "-- Multiple top-level Result Output nodes detected. Keep exactly one output sink connected for SQL generation.",
                Errors: errors);
        }
        NodeViewModel resultOutputNode = topLevelOutputs[0];

        List<NodeViewModel> allCteDefinitions = canvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        List<NodeViewModel> cteDefinitions = [.. QueryCompilationNodeGraphAssembler.CollectRelevantCteDefinitions(canvas, resultOutputNode, allCteDefinitions)];
        Dictionary<string, string> cteDefinitionNamesById = _buildCteDefinitionNameMap(cteDefinitions);

        bool hasProjectedColumns = QueryCompilationNodeGraphAssembler.CollectProjectedPins(canvas, resultOutputNode).Count > 0;

        if (!hasProjectedColumns)
        {
            return new QueryCompilationInputStageResult(
                Snapshot: null,
                ShouldShortCircuit: true,
                ShortCircuitSql: "-- Connect columns via Column List, ColumnSet Merge, directly to Result Output.column, or '*' to Result Output.columns",
                Errors: errors);
        }

        (string fromTable, string? fromWarning) = _resolveFromTable(
            tableNodes,
            cteSourceNodes,
            subqueryNodes,
            cteDefinitionNamesById
        );
        if (!string.IsNullOrWhiteSpace(fromWarning))
            errors.Add(fromWarning);

        var snapshot = new QueryCompilationInputSnapshot(
            tableNodes,
            cteSourceNodes,
            subqueryNodes,
            cteDefinitions,
            cteDefinitionNamesById,
            resultOutputNode,
            fromTable,
            errors);

        return new QueryCompilationInputStageResult(
            Snapshot: snapshot,
            ShouldShortCircuit: false,
            ShortCircuitSql: null,
            Errors: errors);
    }

    private static IReadOnlyList<NodeViewModel> ResolveTopLevelResultOutputNodes(CanvasViewModel canvas)
    {
        IReadOnlyList<NodeViewModel> outputs = canvas.Nodes
            .Where(n => n.Type is NodeType.ResultOutput or NodeType.SelectOutput)
            .ToList();

        return outputs
            .Where(output =>
                !canvas.Connections.Any(c =>
                    c.FromPin.Owner == output
                    && c.ToPin is not null
                    && c.ToPin.Owner.Type == NodeType.CteDefinition
                    && c.ToPin.Name.Equals("query", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}




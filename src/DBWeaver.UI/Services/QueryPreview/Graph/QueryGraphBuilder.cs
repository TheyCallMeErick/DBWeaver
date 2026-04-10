
namespace DBWeaver.UI.Services.QueryPreview;

/// <summary>
/// Builds NodeGraph structures from canvas state and generates SQL.
/// Handles SELECT bindings, WHERE conditions, JOINs, and LIMIT clauses.
/// </summary>
public sealed class QueryGraphBuilder(CanvasViewModel canvas, DatabaseProvider provider)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;
    private readonly QueryPreviewDiagnosticContextEnricher _diagnosticEnricher = new();
    private readonly QueryCompilationGenerationErrorMapper _generationErrorMapper = new();
    private readonly QueryPreviewSqlPreviewFormatter _previewFormatter = new(provider);
    private readonly QueryCompilationCteResolver _cteResolver = new(canvas, provider);
    private readonly QueryCompilationValidationService _validationService = new(canvas, provider);
    private QueryCompilationNodeGraphAssembler? _graphAssembler;
    private QueryCompilationPipelineRunner? _pipelineRunner;

    private QueryCompilationNodeGraphAssembler GraphAssembler =>
        _graphAssembler ??= new QueryCompilationNodeGraphAssembler(_canvas, _provider, _cteResolver);

    private QueryCompilationPipelineRunner PipelineRunner =>
        _pipelineRunner ??= new QueryCompilationPipelineRunner(
            _canvas,
            _provider,
            _cteResolver,
            _validationService,
            GraphAssembler,
            _previewFormatter,
            _generationErrorMapper);

    public bool TryBuildGraphSnapshot(
        out NodeGraph graph,
        out string? fromTable,
        out string? previewSql,
        out List<string> errors)
    {
        graph = new NodeGraph();
        fromTable = null;
        previewSql = null;
        errors = [];

        if (TryResolveReportSql(out string reportSql))
        {
            previewSql = reportSql;
            fromTable = "__raw_sql_report__";
            return true;
        }

        NodeViewModel? resultOutputNode = _canvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.ResultOutput or NodeType.SelectOutput
        );
        if (resultOutputNode is null)
        {
            errors.Add("Add a Result Output node to generate SQL");
            return false;
        }

        List<NodeViewModel> allCteDefinitions = _canvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        List<NodeViewModel> cteDefinitions = [.. QueryCompilationNodeGraphAssembler.CollectRelevantCteDefinitions(_canvas, resultOutputNode, allCteDefinitions)];
        Dictionary<string, string> cteNames = _cteResolver.BuildCteDefinitionNameMap(cteDefinitions);

        graph = GraphAssembler.BuildNodeGraph(resultOutputNode, cteDefinitions, cteNames, includeCtes: true);

        List<NodeViewModel> tableNodes = _canvas.Nodes.Where(n => n.Type == NodeType.TableSource).ToList();
        List<NodeViewModel> cteSourceNodes = _canvas.Nodes.Where(n => n.Type == NodeType.CteSource).ToList();
        List<NodeViewModel> subqueryNodes = _canvas.Nodes
            .Where(n => n.Type is NodeType.Subquery or NodeType.SubqueryReference)
            .ToList();
        (string resolvedFromTable, string? warning) = ResolveFromTable(tableNodes, cteSourceNodes, subqueryNodes, cteNames);
        fromTable = resolvedFromTable;
        if (!string.IsNullOrWhiteSpace(warning))
            errors.Add(warning);

        if (string.IsNullOrWhiteSpace(fromTable))
            return false;

        try
        {
            var svc = QueryGeneratorService.Create(_provider);
            GeneratedQuery result = svc.Generate(graph);
            previewSql = _previewFormatter.InlineBindingsForPreview(result.Sql, result.Bindings);
        }
        catch (Exception ex)
        {
            errors.AddRange(_generationErrorMapper.Map(ex));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds SQL from the current canvas state. Returns (sql, errors).
    /// </summary>
    public (string Sql, List<string> Errors) BuildSql()
    {
        (string sql, List<PreviewDiagnostic> diagnostics) = BuildSqlWithDiagnostics();
        return (sql, diagnostics.Select(d => d.Message).ToList());
    }

    /// <summary>
    /// Builds SQL and returns structured diagnostics with stable categories/codes.
    /// </summary>
    public (string Sql, List<PreviewDiagnostic> Diagnostics) BuildSqlWithDiagnostics()
    {
        (string sql, List<string> legacyMessages) = BuildSqlCore();
        List<PreviewDiagnostic> diagnostics = PreviewDiagnosticMapper.FromLegacyMessages(legacyMessages);
        _diagnosticEnricher.Enrich(_canvas, diagnostics);
        return (sql, diagnostics);
    }

    private (string Sql, List<string> Errors) BuildSqlCore()
    {
        if (TryResolveReportSql(out string reportSql))
            return (reportSql, []);

        return PipelineRunner.Execute();
    }

    private bool TryResolveReportSql(out string sql)
    {
        sql = string.Empty;

        bool hasQueryOutput = _canvas.Nodes.Any(n => n.Type is NodeType.ResultOutput or NodeType.SelectOutput);
        if (hasQueryOutput)
            return false;

        NodeViewModel? reportOutput = _canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.ReportOutput);
        if (reportOutput is null)
            return false;

        ConnectionViewModel? reportInputConnection = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner?.Id == reportOutput.Id
            && c.ToPin.Name.Equals("query", StringComparison.OrdinalIgnoreCase));
        if (reportInputConnection is null)
            return false;

        NodeViewModel? sourceNode = reportInputConnection.FromPin?.Owner;
        if (sourceNode is null || sourceNode.Type != NodeType.RawSqlQuery)
            return false;

        if (sourceNode.Parameters.TryGetValue("sql", out string? configuredSql)
            && !string.IsNullOrWhiteSpace(configuredSql))
        {
            sql = configuredSql.Trim();
            return true;
        }

        if (sourceNode.Parameters.TryGetValue("sql_text", out string? sqlText)
            && !string.IsNullOrWhiteSpace(sqlText))
        {
            sql = sqlText.Trim();
            return true;
        }

        if (sourceNode.PinLiterals.TryGetValue("sql_text", out string? pinLiteral)
            && !string.IsNullOrWhiteSpace(pinLiteral))
        {
            sql = pinLiteral.Trim();
            return true;
        }

        return false;
    }

    private (string FromTable, string? Warning) ResolveFromTable(
        IReadOnlyList<NodeViewModel> tableNodes,
        IReadOnlyList<NodeViewModel> cteSourceNodes,
        IReadOnlyList<NodeViewModel> subqueryNodes,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    ) =>
        new QueryCompilationSourceResolver(_canvas, _cteResolver.ResolveCteSourceReference)
            .ResolveFromTable(tableNodes, cteSourceNodes, subqueryNodes, cteDefinitionNamesById);

}




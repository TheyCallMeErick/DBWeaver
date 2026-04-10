
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationCteResolver(CanvasViewModel canvas, DatabaseProvider provider)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;

    public Dictionary<string, string> BuildCteDefinitionNameMap(
        IReadOnlyList<NodeViewModel> cteDefinitions)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (NodeViewModel definition in cteDefinitions)
        {
            string? name = ResolveDefinitionName(definition);
            if (!string.IsNullOrWhiteSpace(name))
                map[definition.Id] = name;
        }

        return map;
    }

    public string? ResolveFromTableForOutput(
        NodeViewModel resultOutput,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById)
    {
        HashSet<string> upstream = CollectUpstreamNodeIds(resultOutput, includeCtes: false);

        NodeViewModel? table = _canvas.Nodes.FirstOrDefault(n =>
            upstream.Contains(n.Id)
            && n.Type == NodeType.TableSource
        );
        if (table is not null)
            return table.Subtitle ?? table.Title;

        NodeViewModel? cteSource = _canvas.Nodes.FirstOrDefault(n =>
            upstream.Contains(n.Id)
            && n.Type == NodeType.CteSource
        );
        if (cteSource is not null)
            return ResolveCteSourceReference(cteSource, cteDefinitionNamesById);

        return null;
    }

    public string? ResolveDefinitionName(NodeViewModel definition)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, definition, "name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        return ReadCteName(definition.Parameters);
    }

    public string? ResolveSourceTable(NodeViewModel definition)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, definition, "source_table_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        if (
            definition.Parameters.TryGetValue("source_table", out string? sourceTable)
            && !string.IsNullOrWhiteSpace(sourceTable)
        )
        {
            return sourceTable.Trim();
        }

        return null;
    }

    public string? ResolveCteSourceReference(
        NodeViewModel cteSource,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById)
    {
        string? cteName = ResolveCteSourceName(cteSource, cteDefinitionNamesById);
        if (string.IsNullOrWhiteSpace(cteName))
            return null;

        string? alias = ResolveCteSourceAlias(cteSource);
        var expr = new CteReferenceExpr(cteName, alias);
        var emitContext = new EmitContext(_provider, new SqlFunctionRegistry(_provider));
        return expr.Emit(emitContext);
    }

    private string? ResolveCteSourceName(
        NodeViewModel cteSource,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById)
    {
        ConnectionViewModel? byConnection = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == cteSource
            && c.ToPin?.Name == "cte"
            && c.FromPin.Owner.Type == NodeType.CteDefinition
            && cteDefinitionNamesById.ContainsKey(c.FromPin.Owner.Id)
        );

        if (byConnection is not null)
            return cteDefinitionNamesById[byConnection.FromPin.Owner.Id];

        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, cteSource, "cte_name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        string? byParam = ReadCteName(cteSource.Parameters);
        if (!string.IsNullOrWhiteSpace(byParam))
            return byParam;
        return null;
    }

    private string? ResolveCteSourceAlias(NodeViewModel cteSource)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, cteSource, "alias_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput.Trim();

        if (
            cteSource.Parameters.TryGetValue("alias", out string? byParam)
            && !string.IsNullOrWhiteSpace(byParam)
        )
        {
            return byParam.Trim();
        }

        return null;
    }

    private static string? ReadCteName(IReadOnlyDictionary<string, string> parameters)
    {
        if (
            parameters.TryGetValue("name", out string? name)
            && !string.IsNullOrWhiteSpace(name)
        )
        {
            return name.Trim();
        }

        if (
            parameters.TryGetValue("cte_name", out string? legacyName)
            && !string.IsNullOrWhiteSpace(legacyName)
        )
        {
            return legacyName.Trim();
        }

        return null;
    }

    private HashSet<string> CollectUpstreamNodeIds(NodeViewModel sinkNode, bool includeCtes)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sinkNode.Id };
        var queue = new Queue<string>();
        queue.Enqueue(sinkNode.Id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel conn in _canvas.Connections.Where(c => c.ToPin?.Owner.Id == current))
            {
                NodeViewModel fromOwner = conn.FromPin.Owner;
                if (!includeCtes && fromOwner.Type == NodeType.CteDefinition)
                    continue;

                if (visited.Add(fromOwner.Id))
                    queue.Enqueue(fromOwner.Id);
            }
        }

        return visited;
    }
}



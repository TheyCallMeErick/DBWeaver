
namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationAliasAmbiguityValidator(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;

    public void Validate(
        NodeViewModel resultOutputNode,
        IReadOnlyList<NodeViewModel> cteDefinitions,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById,
        List<string> errors)
    {
        HashSet<string> mainScopeNodeIds = CollectUpstreamNodeIds(resultOutputNode, includeCtes: false);
        ValidateAliasAmbiguityForScope("main query scope", mainScopeNodeIds, cteDefinitionNamesById, errors);

        IReadOnlyList<NodeViewModel> cteDefinitionsToValidate = _canvas.Nodes
            .Where(n => n.Type == NodeType.CteDefinition)
            .Concat(cteDefinitions)
            .DistinctBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (NodeViewModel cteDefinition in cteDefinitionsToValidate)
        {
            ConnectionViewModel? queryWire = _canvas.Connections.FirstOrDefault(c =>
                c.ToPin?.Owner == cteDefinition
                && c.ToPin?.Name == "query"
                && c.FromPin.Owner.Type is NodeType.ResultOutput or NodeType.SelectOutput
            );

            if (queryWire?.FromPin.Owner is not NodeViewModel cteOutput)
                continue;

            HashSet<string> cteScopeNodeIds = CollectUpstreamNodeIds(cteOutput, includeCtes: false);
            string cteName = ResolveDefinitionName(cteDefinition) ?? cteDefinition.Id;
            ValidateAliasAmbiguityForScope($"CTE '{cteName}' scope", cteScopeNodeIds, cteDefinitionNamesById, errors);
        }
    }

    private void ValidateAliasAmbiguityForScope(
        string scopeLabel,
        HashSet<string> scopeNodeIds,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById,
        List<string> errors)
    {
        var aliases = _canvas.Nodes
            .Where(n => scopeNodeIds.Contains(n.Id) && IsAliasSourceNodeType(n.Type))
            .Select(node => new
            {
                Node = node,
                Alias = ResolveEffectiveSourceAlias(node, cteDefinitionNamesById),
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Alias))
            .GroupBy(x => x.Alias!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in aliases)
        {
            string sources = string.Join(
                ", ",
                group.Select(x => $"{x.Node.Title}({x.Node.Id[..Math.Min(8, x.Node.Id.Length)]})")
            );

            errors.Add(
                $"Potential alias ambiguity in {scopeLabel}: alias '{group.Key}' is used by multiple sources ({sources}). Use distinct aliases."
            );
        }
    }

    private bool IsAliasSourceNodeType(NodeType nodeType) =>
        nodeType is NodeType.TableSource or NodeType.CteSource or NodeType.Subquery or NodeType.SubqueryReference;

    private string? ResolveEffectiveSourceAlias(
        NodeViewModel node,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById)
    {
        return node.Type switch
        {
            NodeType.TableSource => ResolveEffectiveTableAlias(node),
            NodeType.CteSource => ResolveEffectiveCteSourceAlias(node, cteDefinitionNamesById),
            NodeType.Subquery or NodeType.SubqueryReference => ResolveEffectiveSubqueryAlias(node),
            _ => null,
        };
    }

    private string? ResolveEffectiveTableAlias(NodeViewModel tableNode)
    {
        if (!string.IsNullOrWhiteSpace(tableNode.Alias))
            return tableNode.Alias!.Trim();

        if (tableNode.Parameters.TryGetValue("alias", out string? aliasParam)
            && !string.IsNullOrWhiteSpace(aliasParam))
        {
            return aliasParam.Trim();
        }

        string source = !string.IsNullOrWhiteSpace(tableNode.Subtitle) ? tableNode.Subtitle! : tableNode.Title;
        string[] parts = source.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : parts[^1].Trim();
    }

    private string? ResolveEffectiveCteSourceAlias(
        NodeViewModel cteSourceNode,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById)
    {
        string? explicitAlias = ResolveCteSourceAlias(cteSourceNode);
        if (!string.IsNullOrWhiteSpace(explicitAlias))
            return explicitAlias.Trim();

        string? cteName = ResolveCteSourceName(cteSourceNode, cteDefinitionNamesById);
        return string.IsNullOrWhiteSpace(cteName) ? null : cteName.Trim();
    }

    private static string ResolveEffectiveSubqueryAlias(NodeViewModel subqueryNode)
    {
        string? alias = subqueryNode.Parameters.TryGetValue("alias", out string? aliasParam)
            ? aliasParam
            : null;

        if (string.IsNullOrWhiteSpace(alias))
            return "subq";

        string trimmed = alias.Trim();
        return trimmed.Contains(' ', StringComparison.Ordinal) ? "subq" : trimmed;
    }

    private string? ResolveDefinitionName(NodeViewModel definition)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, definition, "name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        return ReadCteName(definition.Parameters);
    }

    private string? ResolveCteSourceName(
        NodeViewModel cteSource,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, cteSource, "cte_name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        string? byParam = ReadCteName(cteSource.Parameters);
        if (!string.IsNullOrWhiteSpace(byParam))
            return byParam;

        ConnectionViewModel? byConnection = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == cteSource
            && c.ToPin?.Name == "cte"
            && c.FromPin.Owner.Type == NodeType.CteDefinition
            && cteDefinitionNamesById.ContainsKey(c.FromPin.Owner.Id)
        );

        if (byConnection is null)
            return null;

        return cteDefinitionNamesById[byConnection.FromPin.Owner.Id];
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



namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationNodeGraphAssembler(
    CanvasViewModel canvas,
    DatabaseProvider provider,
    QueryCompilationCteResolver cteResolver)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;
    private readonly QueryCompilationCteResolver _cteResolver = cteResolver;

    public NodeGraph BuildNodeGraph(
        NodeViewModel resultOutputNode,
        IReadOnlyList<NodeViewModel> cteDefinitions,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById,
        bool includeCtes
    )
    {
        var scopedNodeIds = CollectUpstreamNodeIds(_canvas, resultOutputNode, includeCtes);

        if (includeCtes && cteDefinitions.Count > 0)
        {
            foreach (NodeViewModel cteDefinition in cteDefinitions)
            {
                scopedNodeIds.Add(cteDefinition.Id);
                foreach (string id in CollectUpstreamNodeIds(_canvas, cteDefinition, includeCtes: false))
                    scopedNodeIds.Add(id);
            }
        }

        if (!includeCtes)
            scopedNodeIds.RemoveWhere(id => _canvas.Nodes.Any(n => n.Id == id && n.Type == NodeType.CteDefinition));

        IncludeRelevantJoinNodes(scopedNodeIds);

        List<NodeViewModel> scopedNodes = _canvas.Nodes
            .Where(n => scopedNodeIds.Contains(n.Id))
            .ToList();

        var nodes = scopedNodes.Select(n => new DBWeaver.Nodes.NodeInstance(
                Id: n.Id,
                Type: n.Type,
                PinLiterals: n.PinLiterals,
                Parameters: n.Parameters,
                Alias: n.Alias,
                TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
                ColumnPins: n.Type is NodeType.TableSource or NodeType.CteSource
                    ? BuildColumnPinsMap(n)
                    : null,
                ColumnPinTypes: n.Type is NodeType.TableSource or NodeType.CteSource
                    ? BuildColumnPinTypesMap(n)
                    : null
            ))
            .ToList();

        var connections = _canvas
            .Connections.Where(c =>
                c.ToPin is not null
                && scopedNodeIds.Contains(c.FromPin.Owner.Id)
                && scopedNodeIds.Contains(c.ToPin!.Owner.Id)
            )
            .Select(c => new DBWeaver.Nodes.Connection(
                c.FromPin.Owner.Id,
                c.FromPin.Name,
                c.ToPin!.Owner.Id,
                c.ToPin.Name
            ))
            .ToList();

        var selectBindings = new List<DBWeaver.Nodes.SelectBinding>();

        selectBindings.AddRange(
            CollectProjectedPins(_canvas, resultOutputNode)
                .Select(pin => new DBWeaver.Nodes.SelectBinding(pin.Owner.Id, pin.Name, pin.Owner.Alias)));

        var whereBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "where")
            .Select(c => new DBWeaver.Nodes.WhereBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        var havingBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "having")
            .Select(c => new DBWeaver.Nodes.HavingBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        var qualifyBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "qualify")
            .Select(c => new DBWeaver.Nodes.QualifyBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        bool distinct =
            resultOutputNode.Parameters.TryGetValue("distinct", out string? distinctRaw)
            && bool.TryParse(distinctRaw, out bool parsedDistinct)
            && parsedDistinct;
        string? queryHints = resultOutputNode.Parameters.TryGetValue("query_hints", out string? rawHints)
            && !string.IsNullOrWhiteSpace(rawHints)
            ? rawHints.Trim()
            : null;
        string pivotMode = resultOutputNode.Parameters.TryGetValue("pivot_mode", out string? rawPivotMode)
            && !string.IsNullOrWhiteSpace(rawPivotMode)
            ? rawPivotMode.Trim().ToUpperInvariant()
            : "NONE";
        string? pivotConfig = resultOutputNode.Parameters.TryGetValue("pivot_config", out string? rawPivotConfig)
            && !string.IsNullOrWhiteSpace(rawPivotConfig)
            ? rawPivotConfig.Trim()
            : null;

        var orderBys = new List<DBWeaver.Nodes.OrderBinding>();
        orderBys.AddRange(
            _canvas.Connections
                .Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin.Name.Equals("order_by", StringComparison.OrdinalIgnoreCase))
                .Select(c => new DBWeaver.Nodes.OrderBinding(c.FromPin.Owner.Id, c.FromPin.Name, Descending: false)));
        orderBys.AddRange(
            _canvas.Connections
                .Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin.Name.Equals("order_by_desc", StringComparison.OrdinalIgnoreCase))
                .Select(c => new DBWeaver.Nodes.OrderBinding(c.FromPin.Owner.Id, c.FromPin.Name, Descending: true)));

        var groupBys = new List<DBWeaver.Nodes.GroupByBinding>();
        groupBys.AddRange(
            _canvas.Connections
                .Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin.Name.Equals("group_by", StringComparison.OrdinalIgnoreCase))
                .Select(c => new DBWeaver.Nodes.GroupByBinding(c.FromPin.Owner.Id, c.FromPin.Name)));

        int? limit = null;
        ConnectionViewModel? topConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "top"
            && c.FromPin.Owner.Type == NodeType.Top
        );
        if (topConn is not null)
        {
            NodeViewModel topNode = topConn.FromPin.Owner;
            ConnectionViewModel? countWire = _canvas.Connections.FirstOrDefault(c =>
                c.ToPin?.Owner == topNode
                && c.ToPin?.Name == "count"
                && c.FromPin.Owner.Type == NodeType.ValueNumber
            );
            if (
                countWire is not null
                && countWire.FromPin.Owner.Parameters.TryGetValue("value", out string? wiredVal)
                && int.TryParse(wiredVal, out int wiredCount)
            )
            {
                limit = wiredCount;
            }
            else if (
                topNode.Parameters.TryGetValue("count", out string? paramVal)
                && int.TryParse(paramVal, out int paramCount)
            )
            {
                limit = paramCount;
            }
            else
            {
                limit = 100;
            }
        }

        IReadOnlyList<NodeViewModel> relevantCteDefinitions = includeCtes
            ? CollectRelevantCteDefinitions(_canvas, resultOutputNode, cteDefinitions)
            : [];

        List<DBWeaver.Nodes.CteBinding> ctes = includeCtes
            ? BuildCtes(relevantCteDefinitions, cteDefinitionNamesById)
            : [];

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
            Ctes = ctes,
            SelectOutputs = selectBindings,
            WhereConditions = whereBindings,
            Havings = havingBindings,
            Qualifies = qualifyBindings,
            QueryHints = queryHints,
            PivotMode = pivotMode,
            PivotConfig = pivotConfig,
            OrderBys = orderBys,
            GroupBys = groupBys,
            Distinct = distinct,
            Limit = limit,
        };
    }

    public static bool IsWildcardProjectionPin(PinViewModel pin)
    {
        return pin.Owner.Type is NodeType.TableSource or NodeType.CteSource
            && pin.EffectiveDataType == PinDataType.ColumnSet
            && pin.Name.Equals("*", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProjectionInputPinName(string? pinName)
    {
        if (string.IsNullOrWhiteSpace(pinName))
            return false;

        return pinName.Equals("columns", StringComparison.OrdinalIgnoreCase)
            || pinName.Equals("metadata", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<PinViewModel> CollectProjectedPins(
        CanvasViewModel canvas,
        NodeViewModel resultOutputNode)
    {
        var projectedPins = new List<PinViewModel>();
        var visitedContainerPins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ConnectionViewModel connection in canvas.Connections.Where(c =>
                     c.ToPin?.Owner == resultOutputNode
                     && (c.ToPin.Name.Equals("column", StringComparison.OrdinalIgnoreCase)
                         || c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase))))
        {
            AppendProjectedPins(canvas, connection.FromPin, projectedPins, visitedContainerPins);
        }

        return projectedPins;
    }

    public static IReadOnlyList<NodeViewModel> CollectRelevantCteDefinitions(
        CanvasViewModel canvas,
        NodeViewModel resultOutputNode,
        IReadOnlyList<NodeViewModel>? allCteDefinitions = null)
    {
        IReadOnlyList<NodeViewModel> definitions = allCteDefinitions
            ?? canvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        if (definitions.Count == 0)
            return [];

        HashSet<string> upstream = CollectUpstreamNodeIds(canvas, resultOutputNode, includeCtes: true);
        Dictionary<string, NodeViewModel> definitionsById = definitions.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, NodeViewModel> definitionsByName = definitions
            .Select(def => (Definition: def, Name: ResolveCteDefinitionName(canvas, def)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Name!, item => item.Definition, StringComparer.OrdinalIgnoreCase);

        var relevantIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (NodeViewModel definition in definitions.Where(def => upstream.Contains(def.Id)))
            relevantIds.Add(definition.Id);

        foreach (NodeViewModel cteSource in canvas.Nodes.Where(n => n.Type == NodeType.CteSource && upstream.Contains(n.Id)))
        {
            NodeViewModel? referencedDefinition = ResolveConnectedDefinition(canvas, cteSource, definitionsById)
                ?? ResolveNamedDefinition(canvas, cteSource, definitionsByName);
            if (referencedDefinition is not null)
                relevantIds.Add(referencedDefinition.Id);
        }

        bool expanded;
        do
        {
            expanded = false;
            foreach (NodeViewModel definition in definitions.Where(def => relevantIds.Contains(def.Id)))
            {
                string? sourceTable = ResolveCteSourceTable(canvas, definition);
                if (string.IsNullOrWhiteSpace(sourceTable))
                    continue;

                string normalized = sourceTable.Trim();
                if (definitionsByName.TryGetValue(normalized, out NodeViewModel? referenced)
                    && relevantIds.Add(referenced.Id))
                {
                    expanded = true;
                }
            }
        } while (expanded);

        return definitions.Where(def => relevantIds.Contains(def.Id)).ToList();
    }

    private void IncludeRelevantJoinNodes(HashSet<string> scopedNodeIds)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (NodeViewModel joinNode in _canvas.Nodes.Where(n => n.Type is NodeType.Join or NodeType.RowSetJoin))
            {
                ConnectionViewModel? left = _canvas.Connections.FirstOrDefault(c =>
                    c.ToPin?.Owner == joinNode && c.ToPin.Name.Equals("left", StringComparison.OrdinalIgnoreCase));
                ConnectionViewModel? right = _canvas.Connections.FirstOrDefault(c =>
                    c.ToPin?.Owner == joinNode && c.ToPin.Name.Equals("right", StringComparison.OrdinalIgnoreCase));

                changed |= scopedNodeIds.Add(joinNode.Id);
                if (left?.FromPin?.Owner is not null)
                    changed |= scopedNodeIds.Add(left.FromPin.Owner.Id);
                if (right?.FromPin?.Owner is not null)
                    changed |= scopedNodeIds.Add(right.FromPin.Owner.Id);

                foreach (string upstream in CollectUpstreamNodeIds(_canvas, joinNode, includeCtes: false))
                    changed |= scopedNodeIds.Add(upstream);
            }
        } while (changed);
    }

    private List<DBWeaver.Nodes.CteBinding> BuildCtes(
        IReadOnlyList<NodeViewModel> cteDefinitions,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    )
    {
        var ctes = new List<DBWeaver.Nodes.CteBinding>();

        foreach (NodeViewModel definition in cteDefinitions)
        {
            string name = _cteResolver.ResolveDefinitionName(definition) ?? "cte_name";

            string fromTable = _cteResolver.ResolveSourceTable(definition) ?? "";

            NodeGraph cteGraph = new();
            ConnectionViewModel? queryWire = _canvas.Connections.FirstOrDefault(c =>
                c.ToPin?.Owner == definition
                && c.ToPin?.Name == "query"
                && c.FromPin.Owner.Type is NodeType.ResultOutput or NodeType.SelectOutput
            );

            if (queryWire?.FromPin.Owner is NodeViewModel cteOutput)
            {
                cteGraph = BuildNodeGraph(
                    cteOutput,
                    cteDefinitions,
                    cteDefinitionNamesById,
                    includeCtes: false
                );

                if (string.IsNullOrWhiteSpace(fromTable))
                    fromTable = _cteResolver.ResolveFromTableForOutput(cteOutput, cteDefinitionNamesById) ?? "";
            }
            else if (TryBuildPersistedCteGraph(definition, out NodeGraph persistedGraph, out string? persistedFromTable))
            {
                cteGraph = persistedGraph;
                if (string.IsNullOrWhiteSpace(fromTable))
                    fromTable = persistedFromTable ?? "";
            }

            if (string.IsNullOrWhiteSpace(fromTable))
                continue;

            bool recursive =
                definition.Parameters.TryGetValue("recursive", out string? recursiveRaw)
                && bool.TryParse(recursiveRaw, out bool recursiveFlag)
                && recursiveFlag;

            ctes.Add(new DBWeaver.Nodes.CteBinding(name, fromTable, cteGraph, recursive));
        }

        return ctes;
    }

    private bool TryBuildPersistedCteGraph(
        NodeViewModel definitionNode,
        out NodeGraph graph,
        out string? fromTable
    )
    {
        graph = new NodeGraph();
        fromTable = null;

        if (!definitionNode.Parameters.TryGetValue(CanvasSerializer.CteSubgraphParameterKey, out string? payload)
            || string.IsNullOrWhiteSpace(payload))
            return false;

        SavedCteSubgraph? subgraph;
        try
        {
            subgraph = System.Text.Json.JsonSerializer.Deserialize<SavedCteSubgraph>(payload);
        }
        catch
        {
            return false;
        }

        if (subgraph is null || subgraph.Nodes.Count == 0)
            return false;

        var tempCanvas = new CanvasViewModel();
        var tempSavedCanvas = new SavedCanvas(
            Version: CanvasSerializer.CurrentCanvasSchemaVersion,
            DatabaseProvider: null,
            ConnectionName: null,
            Zoom: 1,
            PanX: 0,
            PanY: 0,
            Nodes: subgraph.Nodes,
            Connections: subgraph.Connections,
            SelectBindings: [],
            WhereBindings: [],
            AppVersion: CanvasSerializer.AppVersion,
            CreatedAt: DateTime.UtcNow.ToString("o"),
            Description: "temporary cte compilation");

        CanvasLoadResult loadResult = CanvasSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(tempSavedCanvas),
            tempCanvas);
        if (!loadResult.Success)
            return false;

        NodeViewModel? tempResultOutput = tempCanvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.ResultOutput or NodeType.SelectOutput
        );
        if (tempResultOutput is null)
            return false;

        var tempCteResolver = new QueryCompilationCteResolver(tempCanvas, _provider);
        var tempAssembler = new QueryCompilationNodeGraphAssembler(tempCanvas, _provider, tempCteResolver);
        List<NodeViewModel> tempCteDefinitions = tempCanvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        Dictionary<string, string> tempCteDefinitionNamesById = tempCteResolver.BuildCteDefinitionNameMap(tempCteDefinitions);

        graph = tempAssembler.BuildNodeGraph(
            tempResultOutput,
            tempCteDefinitions,
            tempCteDefinitionNamesById,
            includeCtes: false
        );
        fromTable = tempCteResolver.ResolveFromTableForOutput(tempResultOutput, tempCteDefinitionNamesById);
        return true;
    }

    private static Dictionary<string, string> BuildColumnPinsMap(NodeViewModel node)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (PinViewModel pin in node.OutputPins)
        {
            if (string.IsNullOrWhiteSpace(pin.Name)
                || map.ContainsKey(pin.Name)
                || pin.Direction != PinDirection.Output)
                continue;

            map[pin.Name] = pin.Name;
        }

        return map;
    }

    private static Dictionary<string, PinDataType> BuildColumnPinTypesMap(NodeViewModel node)
    {
        var map = new Dictionary<string, PinDataType>(StringComparer.Ordinal);
        foreach (PinViewModel pin in node.OutputPins)
        {
            if (string.IsNullOrWhiteSpace(pin.Name)
                || map.ContainsKey(pin.Name)
                || pin.Direction != PinDirection.Output)
                continue;

            map[pin.Name] = pin.EffectiveDataType;
        }

        return map;
    }

    private static void AppendProjectedPins(
        CanvasViewModel canvas,
        PinViewModel sourcePin,
        ICollection<PinViewModel> projectedPins,
        ISet<string> visitedContainerPins)
    {
        if (IsProjectionContainerPin(sourcePin))
        {
            string key = $"{sourcePin.Owner.Id}::{sourcePin.Name}";
            if (!visitedContainerPins.Add(key))
                return;

            foreach (ConnectionViewModel nested in canvas.Connections.Where(c =>
                         c.ToPin?.Owner == sourcePin.Owner
                         && IsProjectionContainerInputPin(sourcePin.Owner, c.ToPin.Name)))
            {
                AppendProjectedPins(canvas, nested.FromPin, projectedPins, visitedContainerPins);
            }

            return;
        }

        projectedPins.Add(sourcePin);
    }

    private static bool IsProjectionContainerPin(PinViewModel pin) =>
        pin.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
        && pin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder or NodeType.ColumnSetMerge;

    private static bool IsProjectionContainerInputPin(NodeViewModel owner, string pinName)
    {
        if (owner.Type == NodeType.ColumnSetMerge)
            return pinName.Equals("sets", StringComparison.OrdinalIgnoreCase);

        return IsProjectionInputPinName(pinName);
    }

    private static string? ResolveCteDefinitionName(CanvasViewModel canvas, NodeViewModel definition)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(canvas, definition, "name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        if (definition.Parameters.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name))
            return name.Trim();

        if (definition.Parameters.TryGetValue("cte_name", out string? legacyName) && !string.IsNullOrWhiteSpace(legacyName))
            return legacyName.Trim();

        return null;
    }

    private static string? ResolveCteSourceTable(CanvasViewModel canvas, NodeViewModel definition)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(canvas, definition, "source_table_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        if (definition.Parameters.TryGetValue("source_table", out string? sourceTable)
            && !string.IsNullOrWhiteSpace(sourceTable))
        {
            return sourceTable.Trim();
        }

        return null;
    }

    private static NodeViewModel? ResolveConnectedDefinition(
        CanvasViewModel canvas,
        NodeViewModel cteSource,
        IReadOnlyDictionary<string, NodeViewModel> definitionsById)
    {
        ConnectionViewModel? connection = canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == cteSource
            && c.ToPin.Name.Equals("cte", StringComparison.OrdinalIgnoreCase)
            && c.FromPin.Owner.Type == NodeType.CteDefinition);

        if (connection is null)
            return null;

        return definitionsById.TryGetValue(connection.FromPin.Owner.Id, out NodeViewModel? definition)
            ? definition
            : null;
    }

    private static NodeViewModel? ResolveNamedDefinition(
        CanvasViewModel canvas,
        NodeViewModel cteSource,
        IReadOnlyDictionary<string, NodeViewModel> definitionsByName)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(canvas, cteSource, "cte_name_text");
        string? cteName = !string.IsNullOrWhiteSpace(byTextInput)
            ? byTextInput
            : cteSource.Parameters.TryGetValue("cte_name", out string? configuredName) && !string.IsNullOrWhiteSpace(configuredName)
                ? configuredName.Trim()
                : null;

        if (string.IsNullOrWhiteSpace(cteName))
            return null;

        return definitionsByName.TryGetValue(cteName, out NodeViewModel? definition)
            ? definition
            : null;
    }

    private static HashSet<string> CollectUpstreamNodeIds(CanvasViewModel canvas, NodeViewModel sinkNode, bool includeCtes)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sinkNode.Id };
        var queue = new Queue<string>();
        queue.Enqueue(sinkNode.Id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel conn in canvas.Connections.Where(c => c.ToPin?.Owner.Id == current))
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




using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

/// <summary>
/// Builds NodeGraph structures from canvas state and generates SQL.
/// Handles SELECT bindings, WHERE conditions, JOINs, and LIMIT clauses.
/// </summary>
public sealed class QueryGraphBuilder(CanvasViewModel canvas, DatabaseProvider provider)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;

    /// <summary>
    /// Builds SQL from the current canvas state. Returns (sql, errors).
    /// </summary>
    public (string Sql, List<string> Errors) BuildSql()
    {
        var errors = new List<string>();

        // Resolve data sources on canvas.
        var tableNodes = _canvas.Nodes.Where(n => n.Type == NodeType.TableSource).ToList();
        var cteSourceNodes = _canvas.Nodes.Where(n => n.Type == NodeType.CteSource).ToList();
        var subqueryNodes = _canvas.Nodes.Where(n => n.Type == NodeType.Subquery).ToList();
        var cteDefinitions = _canvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        Dictionary<string, string> cteDefinitionNamesById = BuildCteDefinitionNameMap(cteDefinitions);

        if (tableNodes.Count == 0 && cteSourceNodes.Count == 0 && subqueryNodes.Count == 0)
            return ("-- Add a table, CTE source, or Subquery node to start building your query", errors);

        // Require a ResultOutput node — without it nothing is compiled
        NodeViewModel? resultOutputNode = _canvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.ResultOutput or NodeType.SelectOutput
        );
        if (resultOutputNode is null)
            return ("-- Add a Result Output node to generate SQL", errors);

        // Require at least one projection source:
        // 1) ColumnList -> ResultOutput.columns, or
        // 2) direct ColumnRef -> ResultOutput.column.
        ConnectionViewModel? columnListConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "columns"
            && c.FromPin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder
        );
        bool hasColumnListColumns = columnListConn is not null && _canvas.Connections.Any(c =>
            c.ToPin?.Owner == columnListConn.FromPin.Owner
            && c.ToPin?.Name == "columns"
        );

        bool hasDirectColumns = _canvas.Connections.Any(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "column"
        );

        if (!hasColumnListColumns && !hasDirectColumns)
            return (
                "-- Connect columns via Column List (columns) or directly to Result Output.column",
                errors
            );

        NodeGraph graph = BuildNodeGraph(
            resultOutputNode,
            cteDefinitions,
            cteDefinitionNamesById,
            includeCtes: true
        );
        (string fromTable, string? fromWarning) = ResolveFromTable(
            tableNodes,
            cteSourceNodes,
            subqueryNodes,
            cteDefinitionNamesById
        );
        if (!string.IsNullOrWhiteSpace(fromWarning))
            errors.Add(fromWarning);

        var joinResolver = new JoinResolver(_canvas, _provider);
        (List<JoinDefinition> joins, List<string> joinWarnings) = joinResolver.BuildJoins(tableNodes);
        errors.AddRange(joinWarnings);

        var setOpHandler = new SetOperationHandler(_canvas);
        (SetOperationDefinition? setOperation, string? setOperationWarning) = setOpHandler.ResolveSetOperation(resultOutputNode);
        if (!string.IsNullOrWhiteSpace(setOperationWarning))
            errors.Add(setOperationWarning);

        var cteValidator = new CteValidator(_canvas, cteDefinitionNamesById, graph.Ctes);
        cteValidator.Validate(errors);

        var subqueryValidator = new SubqueryValidator(_canvas);
        subqueryValidator.Validate(errors);

        ValidateWindowFunctionNodes(errors);
        ValidateConnectionTypeCompatibility(errors);
        ValidatePredicateNodes(resultOutputNode, errors);
        ValidateComparisonNodes(resultOutputNode, errors);
        ValidateQueryHints(resultOutputNode, errors);
        ValidatePivotSettings(resultOutputNode, errors);

        try
        {
            var svc = QueryGeneratorService.Create(_provider);
            GeneratedQuery result = svc.Generate(fromTable, graph, joins, setOperation);
            string previewSql = InlineBindingsForPreview(result.Sql, result.Bindings);
            return (previewSql, errors);
        }
        catch (Exception ex)
        {
            errors.AddRange(MapGenerationErrors(ex));
            return (JoinResolver.FallbackSql(fromTable, joins), errors);
        }
    }

    private static IEnumerable<string> MapGenerationErrors(Exception ex)
    {
        if (ex is InvalidOperationException && ex.Message.Contains("Cycle detected between CTE definitions", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "CTE cycle detected. Remove circular CTE dependencies or refactor with a base CTE plus recursive CTE.";
            yield break;
        }

        if (ex is InvalidOperationException && ex.Message.Contains("references itself but is not marked recursive", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "CTE self-reference requires the 'recursive' flag enabled on the CTE Definition node.";
            yield break;
        }

        if (ex is NotSupportedException && ex.Message.Contains("requires 'value' input", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "Window function is missing required 'value' input. Connect a value pin for this function type.";
            yield break;
        }

        yield return ex.Message;
    }

    private string InlineBindingsForPreview(string sql, IReadOnlyDictionary<string, object?> bindings)
    {
        if (string.IsNullOrWhiteSpace(sql) || bindings.Count == 0)
            return sql;

        string inlinedSql = sql;

        foreach ((string key, object? value) in bindings.OrderByDescending(k => k.Key.Length))
        {
            string placeholder = key.StartsWith("@", StringComparison.Ordinal)
                || key.StartsWith(":", StringComparison.Ordinal)
                ? key
                : "@" + key;

            string literal = ToSqlLiteral(value);
            string escaped = Regex.Escape(placeholder);
            inlinedSql = Regex.Replace(inlinedSql, $@"(?<![A-Za-z0-9_]){escaped}(?![A-Za-z0-9_])", literal);

            if (!placeholder.StartsWith(":", StringComparison.Ordinal))
            {
                string colonPlaceholder = ":" + placeholder.TrimStart('@');
                string colonEscaped = Regex.Escape(colonPlaceholder);
                inlinedSql = Regex.Replace(inlinedSql, $@"(?<![A-Za-z0-9_]){colonEscaped}(?![A-Za-z0-9_])", literal);
            }
        }

        return inlinedSql;
    }

    private string ToSqlLiteral(object? value)
    {
        if (value is null || value == DBNull.Value)
            return "NULL";

        if (value is bool boolValue)
        {
            return _provider == DatabaseProvider.SqlServer
                ? (boolValue ? "1" : "0")
                : (boolValue ? "TRUE" : "FALSE");
        }

        if (value is string stringValue)
            return "'" + stringValue.Replace("'", "''", StringComparison.Ordinal) + "'";

        if (value is DateTime dateTime)
            return "'" + dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";

        if (value is DateTimeOffset dateTimeOffset)
            return "'" + dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) + "'";

        if (value is Guid guid)
            return "'" + guid.ToString("D", CultureInfo.InvariantCulture) + "'";

        if (value is byte[] bytes)
            return "0x" + Convert.ToHexString(bytes);

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL";

        return "'" + value.ToString()?.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private NodeGraph BuildNodeGraph(
        NodeViewModel resultOutputNode,
        IReadOnlyList<NodeViewModel> cteDefinitions,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById,
        bool includeCtes
    )
    {
        List<NodeViewModel> scopedNodes;
        if (includeCtes)
        {
            scopedNodes = [.. _canvas.Nodes];
        }
        else
        {
            HashSet<string> upstream = CollectUpstreamNodeIds(resultOutputNode, includeCtes: false);
            scopedNodes =
            [
                .. _canvas.Nodes.Where(n =>
                    upstream.Contains(n.Id)
                    && n.Type != NodeType.CteDefinition
                ),
            ];
        }

        var scopedNodeIds = scopedNodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nodes = scopedNodes.Select(n => new VisualSqlArchitect.Nodes.NodeInstance(
                Id: n.Id,
                Type: n.Type,
                PinLiterals: n.PinLiterals,
                Parameters: n.Parameters,
                Alias: n.Alias,
                TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
                ColumnPins: n.Type == NodeType.TableSource
                    ? n.OutputPins.ToDictionary(p => p.Name, p => p.Name)
                    : null,
                ColumnPinTypes: n.Type == NodeType.TableSource
                    ? n.OutputPins.ToDictionary(p => p.Name, p => p.DataType)
                    : null
            ))
            .ToList();

        var connections = _canvas
            .Connections.Where(c =>
                c.ToPin is not null
                && scopedNodeIds.Contains(c.FromPin.Owner.Id)
                && scopedNodeIds.Contains(c.ToPin!.Owner.Id)
            )
            .Select(c => new VisualSqlArchitect.Nodes.Connection(
                c.FromPin.Owner.Id,
                c.FromPin.Name,
                c.ToPin!.Owner.Id,
                c.ToPin.Name
            ))
            .ToList();

        // ── SELECT: ColumnList/ColumnSetBuilder → ResultOutput.columns plus direct ResultOutput.column ──
        ConnectionViewModel? columnListConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "columns"
            && c.FromPin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder
        );

        var selectBindings = new List<VisualSqlArchitect.Nodes.SelectBinding>();

        if (columnListConn?.FromPin.Owner is NodeViewModel columnListNode)
        {
            selectBindings.AddRange(
                _canvas
                    .Connections.Where(c =>
                        c.ToPin?.Owner == columnListNode
                        && c.ToPin?.Name == "columns"
                    )
                    .OrderBy(c => c.FromPin.Name, StringComparer.Ordinal)
                    .Select(c => new VisualSqlArchitect.Nodes.SelectBinding(
                        c.FromPin.Owner.Id,
                        c.FromPin.Name,
                        c.FromPin.Owner.Alias
                    ))
            );
        }

        selectBindings.AddRange(
            _canvas
                .Connections.Where(c =>
                    c.ToPin?.Owner == resultOutputNode
                    && c.ToPin?.Name == "column"
                )
                .Select(c => new VisualSqlArchitect.Nodes.SelectBinding(
                    c.FromPin.Owner.Id,
                    c.FromPin.Name,
                    c.FromPin.Owner.Alias
                ))
        );

        // ── WHERE: only what is connected to ResultOutput.where ──────────────
        var whereBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "where")
            .Select(c => new VisualSqlArchitect.Nodes.WhereBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        // ── HAVING: only what is connected to ResultOutput.having ───────────
        var havingBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "having")
            .Select(c => new VisualSqlArchitect.Nodes.HavingBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        // ── QUALIFY: post-window filtering connected to ResultOutput.qualify ─
        var qualifyBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "qualify")
            .Select(c => new VisualSqlArchitect.Nodes.QualifyBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        bool distinct =
            resultOutputNode.Parameters.TryGetValue("distinct", out string? distinctValue)
            && bool.TryParse(distinctValue, out bool isDistinct)
            && isDistinct;

        string? queryHints = null;
        if (resultOutputNode.Parameters.TryGetValue("query_hints", out string? hintsRaw)
            && QueryHintSyntax.TryNormalize(_provider, hintsRaw, out string normalizedHints, out _)
            && !string.IsNullOrWhiteSpace(normalizedHints))
        {
            queryHints = normalizedHints;
        }

        string pivotMode = resultOutputNode.Parameters.TryGetValue("pivot_mode", out string? pivotModeRaw)
            ? (pivotModeRaw ?? "NONE").Trim().ToUpperInvariant()
            : "NONE";
        if (pivotMode is not ("PIVOT" or "UNPIVOT"))
            pivotMode = "NONE";

        string? pivotConfig = null;
        if (resultOutputNode.Parameters.TryGetValue("pivot_config", out string? pivotConfigRaw))
        {
            string normalizedPivotConfig = (pivotConfigRaw ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedPivotConfig))
                pivotConfig = normalizedPivotConfig;
        }

        var orderBys = new List<VisualSqlArchitect.Nodes.OrderBinding>();
        if (resultOutputNode.Parameters.TryGetValue("import_order_terms", out string? orderTermsRaw)
            && !string.IsNullOrWhiteSpace(orderTermsRaw))
        {
            string[] orderTerms = orderTermsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string orderTerm in orderTerms)
            {
                string[] parts = orderTerm.Split('|', StringSplitOptions.None);
                if (parts.Length != 3)
                    continue;

                string nodeId = parts[0].Trim();
                string pinName = parts[1].Trim();
                bool descending = parts[2].Trim().Equals("DESC", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(nodeId)
                    || string.IsNullOrWhiteSpace(pinName)
                    || !scopedNodeIds.Contains(nodeId))
                {
                    continue;
                }

                orderBys.Add(new VisualSqlArchitect.Nodes.OrderBinding(nodeId, pinName, descending));
            }
        }

        var groupBys = new List<VisualSqlArchitect.Nodes.GroupByBinding>();
        if (resultOutputNode.Parameters.TryGetValue("import_group_terms", out string? groupTermsRaw)
            && !string.IsNullOrWhiteSpace(groupTermsRaw))
        {
            string[] groupTerms = groupTermsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string groupTerm in groupTerms)
            {
                string[] parts = groupTerm.Split('|', StringSplitOptions.None);
                if (parts.Length != 2)
                    continue;

                string nodeId = parts[0].Trim();
                string pinName = parts[1].Trim();

                if (string.IsNullOrWhiteSpace(nodeId)
                    || string.IsNullOrWhiteSpace(pinName)
                    || !scopedNodeIds.Contains(nodeId))
                {
                    continue;
                }

                groupBys.Add(new VisualSqlArchitect.Nodes.GroupByBinding(nodeId, pinName));
            }
        }

        // ── LIMIT: from Top node connected to ResultOutput.top ───────────────
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

        List<VisualSqlArchitect.Nodes.CteBinding> ctes = includeCtes
            ? BuildCtes(cteDefinitions, cteDefinitionNamesById)
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

    private (string FromTable, string? Warning) ResolveFromTable(
        IReadOnlyList<NodeViewModel> tableNodes,
        IReadOnlyList<NodeViewModel> cteSourceNodes,
        IReadOnlyList<NodeViewModel> subqueryNodes,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    )
    {
        if (tableNodes.Count > 0)
            return (tableNodes[0].Subtitle ?? tableNodes[0].Title, null);

        if (cteSourceNodes.Count > 0)
        {
            NodeViewModel cte = cteSourceNodes[0];
            string? cteReference = ResolveCteSourceReference(cte, cteDefinitionNamesById);
            if (!string.IsNullOrWhiteSpace(cteReference))
                return (cteReference, null);
        }

        if (subqueryNodes.Count > 0)
        {
            (string? subqueryFrom, string? warning) = ResolveSubqueryFromSource(subqueryNodes[0]);
            if (!string.IsNullOrWhiteSpace(subqueryFrom))
                return (subqueryFrom, warning);

            return ("cte_name", warning);
        }

        return ("cte_name", null);
    }

    private (string? FromSource, string? Warning) ResolveSubqueryFromSource(NodeViewModel subqueryNode)
    {
        string? query = QueryGraphHelpers.ResolveTextInput(_canvas, subqueryNode, "query_text");
        if (string.IsNullOrWhiteSpace(query)
            && subqueryNode.Parameters.TryGetValue("query", out string? byParam)
            && !string.IsNullOrWhiteSpace(byParam))
        {
            query = byParam;
        }

        if (string.IsNullOrWhiteSpace(query))
            return (null, "Subquery source is missing query SQL. Add a SELECT or WITH query.");

        string body = query.Trim().TrimEnd(';');
        if (!QueryGraphHelpers.LooksLikeSelectStatement(body))
        {
            return (
                null,
                "Subquery source must start with SELECT, WITH, or a parenthesized SELECT. Ignoring Subquery source."
            );
        }

        if (!(body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal)))
            body = $"({body})";

        string? alias = QueryGraphHelpers.ResolveTextInput(_canvas, subqueryNode, "alias_text");
        if (string.IsNullOrWhiteSpace(alias)
            && subqueryNode.Parameters.TryGetValue("alias", out string? aliasParam)
            && !string.IsNullOrWhiteSpace(aliasParam))
        {
            alias = aliasParam;
        }

        string? warning = null;
        if (string.IsNullOrWhiteSpace(alias))
        {
            alias = "subq";
            warning = "Subquery source alias is required. Defaulting alias to 'subq'.";
        }
        else
        {
            alias = alias.Trim();
            if (alias.Contains(' ', StringComparison.Ordinal))
            {
                warning = "Subquery source alias cannot contain spaces. Defaulting alias to 'subq'.";
                alias = "subq";
            }
        }

        return ($"{body} {alias}", warning);
    }

    private List<VisualSqlArchitect.Nodes.CteBinding> BuildCtes(
        IReadOnlyList<NodeViewModel> cteDefinitions,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    )
    {
        var ctes = new List<VisualSqlArchitect.Nodes.CteBinding>();

        List<NodeViewModel> tableNodes = _canvas.Nodes.Where(n => n.Type == NodeType.TableSource).ToList();
        List<NodeViewModel> cteSourceNodes = _canvas.Nodes.Where(n => n.Type == NodeType.CteSource).ToList();

        foreach (NodeViewModel def in cteDefinitions)
        {
            string name = ResolveDefinitionName(def) ?? "cte_name";

            string fromTable = ResolveSourceTable(def) ?? "";

            NodeGraph cteGraph = new NodeGraph();
            ConnectionViewModel? queryWire = _canvas.Connections.FirstOrDefault(c =>
                c.ToPin?.Owner == def
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
                    fromTable = ResolveFromTableForOutput(cteOutput, cteDefinitionNamesById) ?? "";
            }
            else if (TryBuildPersistedCteGraph(def, out NodeGraph persistedGraph, out string? persistedFromTable))
            {
                cteGraph = persistedGraph;
                if (string.IsNullOrWhiteSpace(fromTable))
                    fromTable = persistedFromTable ?? "";
            }

            if (string.IsNullOrWhiteSpace(fromTable))
                continue;

            bool recursive =
                def.Parameters.TryGetValue("recursive", out string? recursiveRaw)
                && bool.TryParse(recursiveRaw, out bool recursiveFlag)
                && recursiveFlag;

            ctes.Add(new VisualSqlArchitect.Nodes.CteBinding(name, fromTable, cteGraph, recursive));
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
        tempCanvas.Nodes.Clear();
        tempCanvas.Connections.Clear();
        tempCanvas.UndoRedo.Clear();

        CanvasSerializer.InsertSubgraph(
            subgraph.Nodes,
            subgraph.Connections,
            tempCanvas,
            new Avalonia.Point(0, 0)
        );

        NodeViewModel? tempResultOutput = tempCanvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.ResultOutput or NodeType.SelectOutput
        );
        if (tempResultOutput is null)
            return false;

        var tempBuilder = new QueryGraphBuilder(tempCanvas, _provider);
        List<NodeViewModel> tempCteDefinitions = tempCanvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        Dictionary<string, string> tempCteDefinitionNamesById = tempBuilder.BuildCteDefinitionNameMap(tempCteDefinitions);

        graph = tempBuilder.BuildNodeGraph(
            tempResultOutput,
            tempCteDefinitions,
            tempCteDefinitionNamesById,
            includeCtes: false
        );
        fromTable = tempBuilder.ResolveFromTableForOutput(tempResultOutput, tempCteDefinitionNamesById);
        return true;
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

    private Dictionary<string, string> BuildCteDefinitionNameMap(
        IReadOnlyList<NodeViewModel> cteDefinitions
    )
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (NodeViewModel def in cteDefinitions)
        {
            string? name = ResolveDefinitionName(def);
            if (!string.IsNullOrWhiteSpace(name))
                map[def.Id] = name;
        }

        return map;
    }

    private string? ResolveCteSourceName(
        NodeViewModel cteSource,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    )
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

    private string? ResolveFromTableForOutput(
        NodeViewModel resultOutput,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    )
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

    private string? ResolveCteSourceReference(
        NodeViewModel cteSource,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    )
    {
        string? cteName = ResolveCteSourceName(cteSource, cteDefinitionNamesById);
        if (string.IsNullOrWhiteSpace(cteName))
            return null;

        string? alias = ResolveCteSourceAlias(cteSource);
        var expr = new CteReferenceExpr(cteName, alias);
        var emitContext = new EmitContext(_provider, new SqlFunctionRegistry(_provider));
        return expr.Emit(emitContext);
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

    private string? ResolveDefinitionName(NodeViewModel def)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, def, "name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        return ReadCteName(def.Parameters);
    }

    private string? ResolveSourceTable(NodeViewModel def)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, def, "source_table_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        if (
            def.Parameters.TryGetValue("source_table", out string? sourceTable)
            && !string.IsNullOrWhiteSpace(sourceTable)
        )
        {
            return sourceTable.Trim();
        }

        return null;
    }

    private void ValidateWindowFunctionNodes(List<string> errors)
    {
        foreach (NodeViewModel node in _canvas.Nodes.Where(n => n.Type == NodeType.WindowFunction))
        {
            string function = node.Parameters.TryGetValue("function", out string? f)
                ? (f ?? "RowNumber").Trim()
                : "RowNumber";

            bool hasValueInput = HasInputConnection(node, "value");
            bool hasOrderInput = HasAnyInputWithPrefix(node, "order_");

            if (RequiresValueInput(function) && !hasValueInput)
            {
                errors.Add($"Window function '{function}' requires a connected 'value' input.");
            }

            if (RequiresOrderInput(function) && !hasOrderInput)
            {
                errors.Add($"Window function '{function}' requires at least one ORDER BY input (order_* pin).");
            }

            if (node.Parameters.TryGetValue("frame", out string? frame)
                && !string.IsNullOrWhiteSpace(frame)
                && !frame.Equals("None", StringComparison.OrdinalIgnoreCase)
                && !hasOrderInput)
            {
                errors.Add("Window frame is configured but no ORDER BY input is connected; frame clause will be ignored.");
            }

            if (node.Parameters.TryGetValue("frame", out string? customFrame)
                && customFrame.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                ValidateWindowFrameOffset(node, "frame_start", "frame_start_offset", errors);
                ValidateWindowFrameOffset(node, "frame_end", "frame_end_offset", errors);
            }

            if (function.Equals("Lag", StringComparison.OrdinalIgnoreCase)
                || function.Equals("Lead", StringComparison.OrdinalIgnoreCase))
            {
                if (node.Parameters.TryGetValue("offset", out string? offsetRaw)
                    && !string.IsNullOrWhiteSpace(offsetRaw)
                    && (!int.TryParse(offsetRaw, out int offset) || offset <= 0))
                {
                    errors.Add($"Window function '{function}' has invalid offset '{offsetRaw}'. Using default offset 1.");
                }
            }

            if (function.Equals("Ntile", StringComparison.OrdinalIgnoreCase)
                && node.Parameters.TryGetValue("ntile_groups", out string? groupsRaw)
                && !string.IsNullOrWhiteSpace(groupsRaw)
                && (!int.TryParse(groupsRaw, out int groups) || groups <= 0))
            {
                errors.Add($"Window function 'Ntile' has invalid ntile_groups '{groupsRaw}'. Using default 4.");
            }
        }
    }

    private bool HasInputConnection(NodeViewModel node, string pinName) =>
        _canvas.Connections.Any(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase)
        );

    private bool HasAnyInputWithPrefix(NodeViewModel node, string prefix) =>
        _canvas.Connections.Any(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );

    private static bool RequiresValueInput(string functionName)
    {
        return functionName.Equals("Lag", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Lead", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("FirstValue", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("LastValue", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("SumOver", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("AvgOver", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("MinOver", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("MaxOver", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresOrderInput(string functionName)
    {
        return functionName.Equals("RowNumber", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Rank", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("DenseRank", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Ntile", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Lag", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("Lead", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("FirstValue", StringComparison.OrdinalIgnoreCase)
            || functionName.Equals("LastValue", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateWindowFrameOffset(
        NodeViewModel node,
        string boundParam,
        string offsetParam,
        List<string> errors)
    {
        if (!node.Parameters.TryGetValue(boundParam, out string? boundRaw)
            || string.IsNullOrWhiteSpace(boundRaw))
        {
            return;
        }

        string bound = boundRaw.Trim();
        if (bound is not ("Preceding" or "Following"))
            return;

        if (!node.Parameters.TryGetValue(offsetParam, out string? offsetRaw)
            || string.IsNullOrWhiteSpace(offsetRaw)
            || !int.TryParse(offsetRaw, out int offset)
            || offset < 0)
        {
            errors.Add($"Window frame bound '{boundParam}' requires non-negative numeric '{offsetParam}'. Using default 1.");
        }
    }

    private void ValidateConnectionTypeCompatibility(List<string> errors)
    {
        foreach (ConnectionViewModel connection in _canvas.Connections)
        {
            if (connection.ToPin is null)
                continue;

            PinDataType fromType = connection.FromPin.EffectiveDataType;
            PinDataType toType = connection.ToPin.EffectiveDataType;

            if (IsProjectionSourceConnection(connection, fromType, toType))
                continue;

            if (ArePinsCompatible(fromType, toType))
                continue;

            string fromRef = $"{connection.FromPin.Owner.Title}.{connection.FromPin.Name}";
            string toRef = $"{connection.ToPin.Owner.Title}.{connection.ToPin.Name}";
            errors.Add(
                $"Incompatible connection: {fromRef} ({fromType}) -> {toRef} ({toType})."
            );
        }
    }

    private void ValidatePredicateNodes(NodeViewModel resultOutputNode, List<string> errors)
    {
        HashSet<string> predicateRoots = _canvas.Connections
            .Where(c => c.ToPin?.Owner == resultOutputNode
                && (c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("having", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("qualify", StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.FromPin.Owner.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (predicateRoots.Count == 0)
            return;

        HashSet<string> predicateNodes = CollectUpstreamFrom(predicateRoots);

        foreach (NodeViewModel node in _canvas.Nodes.Where(n => predicateNodes.Contains(n.Id)))
        {
            if (node.Type is NodeType.And or NodeType.Or)
            {
                int inputCount = CountInputsByPrefix(node, "cond_");
                if (inputCount == 0)
                {
                    errors.Add($"{node.Title} node connected to WHERE/HAVING/QUALIFY has no conditions; it compiles to a constant expression.");
                    continue;
                }

                if (inputCount == 1)
                {
                    errors.Add($"{node.Title} node connected to WHERE/HAVING/QUALIFY has only one condition; node is redundant.");
                }

                continue;
            }

            if (node.Type == NodeType.CompileWhere)
            {
                int inputCount = CountInputsByName(node, "conditions");
                if (inputCount == 0)
                {
                    errors.Add("COMPILE WHERE node connected to WHERE/HAVING/QUALIFY has no conditions; it compiles to TRUE.");
                    continue;
                }

                if (inputCount == 1)
                {
                    errors.Add("COMPILE WHERE node connected to WHERE/HAVING/QUALIFY has only one condition; node is redundant.");
                }
            }
        }
    }

    private void ValidateComparisonNodes(NodeViewModel resultOutputNode, List<string> errors)
    {
        HashSet<string> predicateRoots = _canvas.Connections
            .Where(c => c.ToPin?.Owner == resultOutputNode
                && (c.ToPin.Name.Equals("where", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("having", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("qualify", StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.FromPin.Owner.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (predicateRoots.Count == 0)
            return;

        HashSet<string> predicateNodes = CollectUpstreamFrom(predicateRoots);

        foreach (NodeViewModel node in _canvas.Nodes.Where(n => predicateNodes.Contains(n.Id)))
        {
            switch (node.Type)
            {
                case NodeType.Equals:
                case NodeType.NotEquals:
                case NodeType.GreaterThan:
                case NodeType.GreaterOrEqual:
                case NodeType.LessThan:
                case NodeType.LessOrEqual:
                    ValidateRequiredPins(node, errors, "left", "right");
                    break;

                case NodeType.Between:
                case NodeType.NotBetween:
                    ValidateRequiredPins(node, errors, "value", "low", "high");
                    break;

                case NodeType.IsNull:
                case NodeType.IsNotNull:
                    ValidateRequiredPins(node, errors, "value");
                    break;

                case NodeType.Like:
                    ValidateRequiredPins(node, errors, "text");
                    if (!node.Parameters.TryGetValue("pattern", out string? pattern)
                        || string.IsNullOrWhiteSpace(pattern))
                    {
                        errors.Add("LIKE node connected to WHERE/HAVING/QUALIFY has empty pattern parameter.");
                    }
                    break;
            }
        }
    }

    private void ValidateQueryHints(NodeViewModel resultOutputNode, List<string> errors)
    {
        if (!resultOutputNode.Parameters.TryGetValue("query_hints", out string? rawHints))
            return;

        if (string.IsNullOrWhiteSpace(rawHints))
            return;

        if (!QueryHintSyntax.TryNormalize(_provider, rawHints, out _, out string? validationError))
        {
            errors.Add(validationError ?? "Invalid query hints configuration.");
        }
    }

    private void ValidatePivotSettings(NodeViewModel resultOutputNode, List<string> errors)
    {
        string mode = resultOutputNode.Parameters.TryGetValue("pivot_mode", out string? modeRaw)
            ? (modeRaw ?? "NONE").Trim().ToUpperInvariant()
            : "NONE";

        if (mode is not ("PIVOT" or "UNPIVOT"))
            return;

        if (_provider != DatabaseProvider.SqlServer)
        {
            errors.Add("PIVOT/UNPIVOT is currently applied only for SQL Server. Configuration will be ignored for this provider.");
            return;
        }

        string config = resultOutputNode.Parameters.TryGetValue("pivot_config", out string? configRaw)
            ? (configRaw ?? string.Empty).Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(config))
        {
            errors.Add("Pivot mode is enabled but 'pivot_config' is empty.");
            return;
        }

        if (config.Contains(';', StringComparison.Ordinal))
            errors.Add("Pivot configuration contains ';'. Use only the PIVOT/UNPIVOT body expression.");
    }

    private void ValidateRequiredPins(NodeViewModel node, List<string> errors, params string[] pinNames)
    {
        foreach (string pinName in pinNames)
        {
            bool hasConnection = _canvas.Connections.Any(c =>
                c.ToPin?.Owner == node
                && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase)
            );

            bool hasLiteral = node.PinLiterals.TryGetValue(pinName, out string? literal)
                && !string.IsNullOrWhiteSpace(literal);

            if (!hasConnection && !hasLiteral)
            {
                errors.Add($"{node.Title} node connected to WHERE/HAVING/QUALIFY is missing required input '{pinName}'.");
            }
        }
    }

    private HashSet<string> CollectUpstreamFrom(HashSet<string> startNodeIds)
    {
        var visited = new HashSet<string>(startNodeIds, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(startNodeIds);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel connection in _canvas.Connections.Where(c => c.ToPin?.Owner.Id == current))
            {
                string upstream = connection.FromPin.Owner.Id;
                if (visited.Add(upstream))
                    queue.Enqueue(upstream);
            }
        }

        return visited;
    }

    private int CountInputsByPrefix(NodeViewModel node, string pinPrefix) =>
        _canvas.Connections.Count(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.StartsWith(pinPrefix, StringComparison.OrdinalIgnoreCase)
        );

    private int CountInputsByName(NodeViewModel node, string pinName) =>
        _canvas.Connections.Count(c =>
            c.ToPin?.Owner == node
            && c.ToPin.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase)
        );

    private static bool IsProjectionSourceConnection(
        ConnectionViewModel connection,
        PinDataType source,
        PinDataType target
    )
    {
        return source == PinDataType.RowSet
            && target == PinDataType.ColumnRef
            && connection.ToPin is not null
            && connection.ToPin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder
            && connection.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ArePinsCompatible(PinDataType source, PinDataType target)
    {
        if (IsStructuralMismatch(source, target))
            return false;

        if (source.IsNumericScalar() && target.IsNumericScalar())
            return true;

        if (source == PinDataType.ColumnRef && (target.IsScalar() || target == PinDataType.Expression))
            return true;

        if (target == PinDataType.ColumnRef && (source.IsScalar() || source == PinDataType.Expression))
            return true;

        if (source == PinDataType.Expression && target.IsScalar())
            return true;

        if (target == PinDataType.Expression && source.IsScalar())
            return true;

        return source == target;
    }

    private static bool IsStructuralMismatch(PinDataType from, PinDataType to)
    {
        bool fromRowSet = from == PinDataType.RowSet;
        bool toRowSet = to == PinDataType.RowSet;
        return fromRowSet != toRowSet;
    }

}

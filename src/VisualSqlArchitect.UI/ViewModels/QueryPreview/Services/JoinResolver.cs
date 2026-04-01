using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

public sealed class JoinResolver
{
    private readonly CanvasViewModel _canvas;
    private readonly DatabaseProvider _provider;

    public JoinResolver(CanvasViewModel canvas, DatabaseProvider provider)
    {
        _canvas = canvas;
        _provider = provider;
    }

    public (List<JoinDefinition> Joins, List<string> Warnings) BuildJoins(IReadOnlyList<NodeViewModel> tableNodes)
    {
        var joins = new List<JoinDefinition>();
        var warnings = new List<string>();

        List<NodeViewModel> explicitJoinNodes = _canvas.Nodes.Where(n => n.Type == NodeType.Join).ToList();
        foreach (NodeViewModel joinNode in explicitJoinNodes)
        {
            if (TryBuildExplicitJoin(joinNode, out JoinDefinition? join, out string? warning))
                joins.Add(join);

            if (!string.IsNullOrWhiteSpace(warning))
                warnings.Add(warning);
        }

        if (joins.Count > 0)
            return (joins, warnings);

        if (tableNodes.Count <= 1)
            return ([], warnings);

        foreach (ConnectionViewModel conn in _canvas.Connections)
        {
            if (conn.FromPin.Owner.Type != NodeType.TableSource)
                continue;
            if (conn.ToPin?.Owner.Type != NodeType.TableSource)
                continue;

            string left = $"{conn.FromPin.Owner.Subtitle}.{conn.FromPin.Name}";
            string right = $"{conn.ToPin.Owner.Subtitle}.{conn.ToPin.Name}";
            joins.Add(
                new JoinDefinition(
                    conn.ToPin.Owner.Subtitle ?? conn.ToPin.Owner.Title,
                    left,
                    right,
                    "LEFT"
                )
            );
        }

        return (joins, warnings);
    }

    private bool TryBuildExplicitJoin(NodeViewModel joinNode, out JoinDefinition join, out string? warning)
    {
        join = default!;
        warning = null;

        ConnectionViewModel? conditionConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == joinNode
            && c.ToPin?.Name == "condition"
        );

        if (conditionConn is not null
            && TryResolveJoinCondition(conditionConn.FromPin.Owner, out string leftFromCondition, out string rightFromCondition, out string opFromCondition))
        {
            string rightTableFromCondition = ExtractTableReference(rightFromCondition);
            if (string.IsNullOrWhiteSpace(rightTableFromCondition))
            {
                warning =
                    "Join condition is connected, but its right side is not a qualified column reference. Falling back to left/right pins.";
            }
            else
            {
                string joinTypeFromCondition = ResolveJoinType(joinNode, out string? typeWarning);
                if (!string.IsNullOrWhiteSpace(typeWarning))
                    warning = typeWarning;

                join = new JoinDefinition(
                    rightTableFromCondition,
                    leftFromCondition,
                    rightFromCondition,
                    joinTypeFromCondition,
                    opFromCondition
                );

                return true;
            }
        }

        if (conditionConn is not null && TryBuildExplicitJoinFromConditionSql(joinNode, conditionConn.FromPin.Owner, out join, out warning))
            return true;

        ConnectionViewModel? leftConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == joinNode
            && c.ToPin?.Name == "left"
        );

        ConnectionViewModel? rightConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == joinNode
            && c.ToPin?.Name == "right"
        );

        if (leftConn is null || rightConn is null)
        {
            if (TryBuildExplicitJoinFromParameters(joinNode, out join, out warning))
                return true;

            warning =
                "Join node is incomplete: connect a valid 'condition' input, connect both 'left' and 'right' inputs, or fill right_source/left_expr/right_expr parameters. Ignoring explicit join node.";
            return false;
        }

        if (!TryResolveJoinOperand(leftConn.FromPin.Owner, leftConn.FromPin.Name, out string left))
        {
            warning = "Join left input cannot be resolved to a SQL expression.";
            return false;
        }

        if (!TryResolveJoinOperand(rightConn.FromPin.Owner, rightConn.FromPin.Name, out string right))
        {
            warning = "Join right input cannot be resolved to a SQL expression.";
            return false;
        }

        string rightTable = ExtractTableReference(right);
        if (string.IsNullOrWhiteSpace(rightTable))
        {
            warning = "Join right input must resolve to a qualified expression (source.column).";
            return false;
        }

        string joinType = ResolveJoinType(joinNode, out warning);
        string joinOperator = ResolveJoinOperator(joinNode);

        join = new JoinDefinition(
            rightTable,
            left,
            right,
            joinType,
            joinOperator
        );

        return true;
    }

    private bool TryBuildExplicitJoinFromParameters(NodeViewModel joinNode, out JoinDefinition join, out string? warning)
    {
        join = default!;
        warning = null;

        string rightSource = joinNode.Parameters.TryGetValue("right_source", out string? target)
            ? (target ?? string.Empty).Trim()
            : string.Empty;
        string leftExpr = joinNode.Parameters.TryGetValue("left_expr", out string? left)
            ? (left ?? string.Empty).Trim()
            : string.Empty;
        string rightExpr = joinNode.Parameters.TryGetValue("right_expr", out string? right)
            ? (right ?? string.Empty).Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(rightSource)
            || string.IsNullOrWhiteSpace(leftExpr)
            || string.IsNullOrWhiteSpace(rightExpr))
        {
            return false;
        }

        string joinType = ResolveJoinType(joinNode, out warning);
        string joinOperator = ResolveJoinOperator(joinNode);

        join = new JoinDefinition(
            rightSource,
            leftExpr,
            rightExpr,
            joinType,
            joinOperator
        );

        return true;
    }

    private bool TryBuildExplicitJoinFromConditionSql(
        NodeViewModel joinNode,
        NodeViewModel conditionNode,
        out JoinDefinition join,
        out string? warning)
    {
        join = default!;
        warning = null;

        string rightSource = joinNode.Parameters.TryGetValue("right_source", out string? target)
            ? (target ?? string.Empty).Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(rightSource))
        {
            warning =
                "Join condition supports arbitrary boolean expressions when 'right_source' is set on Join node.";
            return false;
        }

        string? onRaw = TryCompileNodeExpressionToSql(conditionNode, "result");
        if (string.IsNullOrWhiteSpace(onRaw))
        {
            warning = "Failed to compile JOIN condition expression. Falling back to standard join wiring.";
            return false;
        }

        string joinType = ResolveJoinType(joinNode, out string? typeWarning);
        if (!string.IsNullOrWhiteSpace(typeWarning))
            warning = typeWarning;

        join = new JoinDefinition(
            rightSource,
            "1",
            "1",
            joinType,
            "=",
            onRaw
        );

        return true;
    }

    private string? TryCompileNodeExpressionToSql(NodeViewModel node, string pinName)
    {
        try
        {
            List<NodeViewModel> allNodes = [.. _canvas.Nodes];
            HashSet<string> allNodeIds = allNodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var graphNodes = allNodes.Select(n => new VisualSqlArchitect.Nodes.NodeInstance(
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

            var graphConnections = _canvas.Connections
                .Where(c => c.ToPin is not null
                    && allNodeIds.Contains(c.FromPin.Owner.Id)
                    && allNodeIds.Contains(c.ToPin!.Owner.Id))
                .Select(c => new VisualSqlArchitect.Nodes.Connection(
                    c.FromPin.Owner.Id,
                    c.FromPin.Name,
                    c.ToPin!.Owner.Id,
                    c.ToPin.Name
                ))
                .ToList();

            var graph = new NodeGraph
            {
                Nodes = graphNodes,
                Connections = graphConnections,
                SelectOutputs = [new VisualSqlArchitect.Nodes.SelectBinding(node.Id, pinName)],
            };

            var emit = new EmitContext(_provider, new SqlFunctionRegistry(_provider));
            var compiler = new NodeGraphCompiler(graph, emit);
            CompiledNodeGraph compiled = compiler.Compile();
            if (compiled.SelectExprs.Count == 0)
                return null;

            return compiled.SelectExprs[0].Expr.Emit(emit);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveJoinType(NodeViewModel joinNode, out string? warning)
    {
        warning = null;

        string joinType = joinNode.Parameters.TryGetValue("join_type", out string? configuredJoinType)
            && !string.IsNullOrWhiteSpace(configuredJoinType)
            ? configuredJoinType.Trim().ToUpperInvariant()
            : "INNER";

        if (joinType is not ("INNER" or "LEFT" or "RIGHT" or "FULL" or "CROSS"))
        {
            warning =
                $"Join type '{joinType}' is not supported. Allowed values: INNER, LEFT, RIGHT, FULL, CROSS. Defaulting to INNER.";
            return "INNER";
        }

        return joinType;
    }

    private bool TryResolveJoinCondition(
        NodeViewModel conditionNode,
        out string left,
        out string right,
        out string op
    )
    {
        left = string.Empty;
        right = string.Empty;
        op = "=";

        op = conditionNode.Type switch
        {
            NodeType.Equals => "=",
            NodeType.NotEquals => "<>",
            NodeType.GreaterThan => ">",
            NodeType.GreaterOrEqual => ">=",
            NodeType.LessThan => "<",
            NodeType.LessOrEqual => "<=",
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(op))
            return false;

        ConnectionViewModel? leftConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == conditionNode
            && c.ToPin?.Name == "left"
        );

        ConnectionViewModel? rightConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == conditionNode
            && c.ToPin?.Name == "right"
        );

        if (leftConn is null || rightConn is null)
            return false;

        return TryResolveJoinOperand(leftConn.FromPin.Owner, leftConn.FromPin.Name, out left)
            && TryResolveJoinOperand(rightConn.FromPin.Owner, rightConn.FromPin.Name, out right);
    }

    private bool TryResolveJoinOperand(NodeViewModel owner, string pinName, out string expr)
    {
        expr = string.Empty;

        if (owner.Type == NodeType.TableSource)
        {
            string table = owner.Subtitle ?? owner.Title;
            expr = $"{table}.{pinName}";
            return true;
        }

        if (owner.Type == NodeType.CteSource)
        {
            var cteDefinitions = _canvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
            var cteDefinitionNamesById = BuildCteDefinitionNameMap(cteDefinitions);
            string? cteRef = ResolveCteSourceReference(owner, cteDefinitionNamesById);
            if (string.IsNullOrWhiteSpace(cteRef) || pinName.Equals("result", StringComparison.OrdinalIgnoreCase))
                return false;

            expr = $"{cteRef}.{pinName}";
            return true;
        }

        if (owner.Type == NodeType.Subquery)
        {
            (string? fromSource, _) = ResolveSubqueryFromSource(owner);
            if (string.IsNullOrWhiteSpace(fromSource) || pinName.Equals("result", StringComparison.OrdinalIgnoreCase))
                return false;

            string alias = ExtractSubqueryAlias(fromSource);
            if (string.IsNullOrWhiteSpace(alias))
                return false;

            expr = $"{alias}.{pinName}";
            return true;
        }

        return false;
    }

    private static string ExtractSubqueryAlias(string fromSource)
    {
        if (string.IsNullOrWhiteSpace(fromSource))
            return string.Empty;

        string trimmed = fromSource.Trim();
        int lastClose = trimmed.LastIndexOf(')');
        if (lastClose < 0 || lastClose >= trimmed.Length - 1)
            return string.Empty;

        return trimmed[(lastClose + 1)..].Trim();
    }

    private static string ResolveJoinOperator(NodeViewModel joinNode)
    {
        if (!joinNode.Parameters.TryGetValue("operator", out string? configured)
            || string.IsNullOrWhiteSpace(configured))
        {
            return "=";
        }

        return configured.Trim() switch
        {
            "=" or "<>" or ">" or ">=" or "<" or "<=" => configured.Trim(),
            _ => "=",
        };
    }

    private static string ExtractTableReference(string qualifiedColumn)
    {
        if (string.IsNullOrWhiteSpace(qualifiedColumn))
            return string.Empty;

        int lastDot = qualifiedColumn.LastIndexOf('.');
        if (lastDot <= 0)
            return string.Empty;

        return qualifiedColumn[..lastDot];
    }

    internal static string FallbackSql(string from, List<JoinDefinition> joins)
    {
        var sb = new System.Text.StringBuilder($"SELECT *\nFROM {from}");

        foreach (JoinDefinition j in joins)
            sb.Append($"\n{j.Type} JOIN {j.TargetTable} ON {j.LeftColumn} = {j.RightColumn}");

        return sb.ToString();
    }

    // Helper methods needed by TryResolveJoinOperand

    private Dictionary<string, string> BuildCteDefinitionNameMap(IReadOnlyList<NodeViewModel> cteDefinitions)
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

    private string? ResolveDefinitionName(NodeViewModel def)
    {
        string? byTextInput = QueryGraphHelpers.ResolveTextInput(_canvas, def, "name_text");
        if (!string.IsNullOrWhiteSpace(byTextInput))
            return byTextInput;

        return ReadCteName(def.Parameters);
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
}

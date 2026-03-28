using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;

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

        // Require at least one TableSource node
        var tableNodes = _canvas.Nodes.Where(n => n.Type == NodeType.TableSource).ToList();

        if (tableNodes.Count == 0)
            return ("-- Add a table node to start building your query", errors);

        // Require a ResultOutput node — without it nothing is compiled
        NodeViewModel? resultOutputNode = _canvas.Nodes.FirstOrDefault(n =>
            n.Type == NodeType.ResultOutput
        );
        if (resultOutputNode is null)
            return ("-- Add a Result Output node to generate SQL", errors);

        // Require a ColumnList connected to ResultOutput.columns with at least one column
        ConnectionViewModel? columnListConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "columns"
            && c.FromPin.Owner.Type == NodeType.ColumnList
        );
        if (columnListConn is null)
            return ("-- Connect a Column List to Result Output to define SELECT columns", errors);

        bool hasColumns = _canvas.Connections.Any(c =>
            c.ToPin?.Owner == columnListConn.FromPin.Owner
            && c.ToPin?.Name == "columns"
        );
        if (!hasColumns)
            return ("-- Connect at least one column to the Column List", errors);

        NodeGraph graph = BuildNodeGraph(resultOutputNode);
        string fromTable = tableNodes[0].Subtitle ?? tableNodes[0].Title;
        List<JoinDefinition> joins = BuildJoins(tableNodes);

        try
        {
            var svc = QueryGeneratorService.Create(_provider);
            GeneratedQuery result = svc.Generate(fromTable, graph, joins);
            return (result.Sql, errors);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            return (FallbackSql(tableNodes, joins), errors);
        }
    }

    private NodeGraph BuildNodeGraph(NodeViewModel resultOutputNode)
    {
        var nodes = _canvas
            .Nodes.Select(n => new VisualSqlArchitect.Nodes.NodeInstance(
                Id: n.Id,
                Type: n.Type,
                PinLiterals: n.PinLiterals,
                Parameters: n.Parameters,
                Alias: n.Alias,
                TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
                ColumnPins: n.Type == NodeType.TableSource
                    ? n.OutputPins.ToDictionary(p => p.Name, p => p.Name)
                    : null
            ))
            .ToList();

        var connections = _canvas
            .Connections.Where(c => c.ToPin is not null)
            .Select(c => new VisualSqlArchitect.Nodes.Connection(
                c.FromPin.Owner.Id,
                c.FromPin.Name,
                c.ToPin!.Owner.Id,
                c.ToPin.Name
            ))
            .ToList();

        // ── SELECT: driven exclusively by ColumnList → ResultOutput.columns ──
        ConnectionViewModel? columnListConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "columns"
            && c.FromPin.Owner.Type == NodeType.ColumnList
        );

        List<VisualSqlArchitect.Nodes.SelectBinding> selectBindings;

        if (columnListConn?.FromPin.Owner is NodeViewModel columnListNode)
        {
            selectBindings =
            [
                .. _canvas
                    .Connections.Where(c =>
                        c.ToPin?.Owner == columnListNode
                        && c.ToPin?.Name == "columns"
                    )
                    .OrderBy(c => c.FromPin.Name, StringComparer.Ordinal)
                    .Select(c => new VisualSqlArchitect.Nodes.SelectBinding(
                        c.FromPin.Owner.Id,
                        c.FromPin.Name,
                        c.FromPin.Owner.Alias
                    )),
            ];
        }
        else
        {
            selectBindings = [];
        }

        // ── WHERE: only what is connected to ResultOutput.where ──────────────
        var whereBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "where")
            .Select(c => new VisualSqlArchitect.Nodes.WhereBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

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

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
            SelectOutputs = selectBindings,
            WhereConditions = whereBindings,
            Limit = limit,
        };
    }

    private List<JoinDefinition> BuildJoins(List<NodeViewModel> tableNodes)
    {
        if (tableNodes.Count <= 1)
            return [];

        var joins = new List<JoinDefinition>();

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

        return joins;
    }

    private static string FallbackSql(List<NodeViewModel> tables, List<JoinDefinition> joins)
    {
        string from = tables[0].Subtitle ?? tables[0].Title;
        var sb = new System.Text.StringBuilder($"SELECT *\nFROM {from}");

        foreach (JoinDefinition j in joins)
            sb.Append($"\n{j.Type} JOIN {j.TargetTable} ON {j.LeftColumn} = {j.RightColumn}");

        return sb.ToString();
    }
}

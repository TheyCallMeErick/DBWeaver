using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

// ─── Conversion report item ───────────────────────────────────────────────────

public enum ImportItemStatus
{
    Imported,
    Partial,
    Skipped,
}

public sealed class ImportReportItem(string label, ImportItemStatus status, string? note = null)
{
    public string Label { get; } = label;
    public ImportItemStatus Status { get; } = status;
    public string? Note { get; } = note;

    public bool IsImported => Status == ImportItemStatus.Imported;
    public bool IsPartial => Status == ImportItemStatus.Partial;
    public bool IsSkipped => Status == ImportItemStatus.Skipped;

    public string StatusIcon =>
        Status switch
        {
            ImportItemStatus.Imported => "✓",
            ImportItemStatus.Partial => "~",
            ImportItemStatus.Skipped => "✗",
            _ => "?",
        };

    public string StatusColor =>
        Status switch
        {
            ImportItemStatus.Imported => "#34D399",
            ImportItemStatus.Partial => "#FBBF24",
            ImportItemStatus.Skipped => "#F87171",
            _ => "#8B95A8",
        };
}

// ─── SQL Importer ─────────────────────────────────────────────────────────────

/// <summary>
/// Overlay view model that accepts a raw SQL SELECT statement and generates
/// an equivalent visual node graph on the canvas.
///
/// Supported: FROM, JOIN, WHERE (simple equality / comparison), LIMIT / TOP,
///            SELECT column list (or *), column aliases.
/// Partial:   Complex WHERE expressions (spawned as a raw note), ORDER BY.
/// Skipped:   Sub-queries, HAVING, aggregate functions, CTEs, UNION.
/// </summary>
public sealed class SqlImporterViewModel(CanvasViewModel canvas) : ViewModelBase
{
    private readonly CanvasViewModel _canvas = canvas;

    private bool _isVisible;
    private bool _isImporting;
    private string _sqlInput = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _hasReport;

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public bool IsImporting
    {
        get => _isImporting;
        private set => Set(ref _isImporting, value);
    }

    public string SqlInput
    {
        get => _sqlInput;
        set => Set(ref _sqlInput, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public bool HasReport
    {
        get => _hasReport;
        private set => Set(ref _hasReport, value);
    }

    public ObservableCollection<ImportReportItem> Report { get; } = [];

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Open()
    {
        SqlInput = string.Empty;
        Report.Clear();
        HasReport = false;
        StatusMessage = string.Empty;
        IsVisible = true;
    }

    public void Close()
    {
        IsVisible = false;
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SqlInput))
        {
            StatusMessage = "Paste a SELECT statement above, then click Import.";
            return;
        }

        IsImporting = true;
        StatusMessage = "Parsing SQL…";
        Report.Clear();
        HasReport = false;

        await Task.Delay(80); // yield to update UI before heavy work

        try
        {
            (int imported, int partial, int skipped) = BuildGraph(SqlInput.Trim(), Report);
            StatusMessage =
                $"Done — {imported} imported, {partial} partial, {skipped} skipped.";
            HasReport = true;
            if (imported + partial > 0)
                Close();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Parse error: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    // ── Graph builder ─────────────────────────────────────────────────────────

    private (int imported, int partial, int skipped) BuildGraph(
        string sql,
        ObservableCollection<ImportReportItem> report
    )
    {
        int imported = 0,
            partial = 0,
            skipped = 0;

        // ── 1. Strip comments ────────────────────────────────────────────────
        sql = Regex.Replace(sql, @"--[^\n]*", " ");
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"\s+", " ").Trim();

        // ── 2. Detect unsupported constructs ─────────────────────────────────
        if (
            Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(sql, @"\(SELECT\b", RegexOptions.IgnoreCase)
        )
        {
            report.Add(
                new ImportReportItem(
                    "CTE / sub-query",
                    ImportItemStatus.Skipped,
                    "CTEs and sub-queries are not supported"
                )
            );
            skipped++;
        }

        if (Regex.IsMatch(sql, @"\bUNION\b", RegexOptions.IgnoreCase))
        {
            report.Add(
                new ImportReportItem("UNION", ImportItemStatus.Skipped, "UNION is not supported")
            );
            skipped++;
        }

        if (Regex.IsMatch(sql, @"\bHAVING\b", RegexOptions.IgnoreCase))
        {
            report.Add(
                new ImportReportItem(
                    "HAVING clause",
                    ImportItemStatus.Skipped,
                    "HAVING is not supported — remove and re-import"
                )
            );
            skipped++;
        }

        // ── 3. Parse SELECT columns ───────────────────────────────────────────
        Match selMatch = Regex.Match(
            sql,
            @"SELECT\s+(DISTINCT\s+)?(.+?)\s+FROM\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!selMatch.Success)
            throw new InvalidOperationException("Could not find SELECT … FROM in the query.");

        bool isDistinct = selMatch.Groups[1].Success;
        string colPart = selMatch.Groups[2].Value.Trim();

        var selectedCols = new List<(string Expr, string? Alias)>();
        bool isStar = colPart == "*";

        if (!isStar)
        {
            foreach (string raw in SplitCommas(colPart))
            {
                string col = raw.Trim();
                Match asMatch = Regex.Match(col, @"^(.+?)\s+AS\s+(\w+)$", RegexOptions.IgnoreCase);
                if (asMatch.Success)
                    selectedCols.Add((asMatch.Groups[1].Value.Trim(), asMatch.Groups[2].Value));
                else
                    selectedCols.Add((col, null));
            }
        }

        // ── 4. Parse FROM / JOINs ─────────────────────────────────────────────
        // Everything from FROM to (WHERE | ORDER BY | LIMIT | TOP | GROUP BY | $)
        Match fromBlock = Regex.Match(
            sql,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|GROUP\s+BY|$))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!fromBlock.Success)
            throw new InvalidOperationException("Could not parse FROM clause.");

        string fromSql = fromBlock.Groups[1].Value.Trim();

        // Split off JOIN clauses
        string[] joinKeywords = ["INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "JOIN"];
        var fromParts = new List<(string Table, string? JoinType, string? OnClause)>();

        // Primary table (before first JOIN)
        int firstJoinIdx = -1;
        string upperFrom = fromSql.ToUpperInvariant();
        foreach (string jk in joinKeywords)
        {
            int idx = upperFrom.IndexOf(jk, StringComparison.Ordinal);
            if (idx >= 0 && (firstJoinIdx < 0 || idx < firstJoinIdx))
                firstJoinIdx = idx;
        }

        string primaryPart =
            firstJoinIdx >= 0 ? fromSql[..firstJoinIdx].Trim() : fromSql.Trim();
        string primaryTable = ExtractTableName(primaryPart);
        fromParts.Add((primaryTable, null, null));

        // JOIN clauses
        var joinMatches = Regex.Matches(
            fromSql,
            @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+(\w+(?:\.\w+)?)(?:\s+(?:AS\s+)?\w+)?\s+ON\s+(.+?)(?=\s+(?:INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+JOIN|JOIN|$))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        foreach (Match jm in joinMatches)
        {
            string jTable = jm.Groups[1].Value.Trim();
            string onClause = jm.Groups[2].Value.Trim();
            string jType = Regex.Match(
                    jm.Value,
                    @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)",
                    RegexOptions.IgnoreCase
                )
                .Value.Trim()
                .ToUpperInvariant();
            fromParts.Add((jTable, jType, onClause));
        }

        // ── 5. Parse WHERE ────────────────────────────────────────────────────
        Match whereMatch = Regex.Match(
            sql,
            @"WHERE\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP|GROUP\s+BY|$))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? whereClause = whereMatch.Success ? whereMatch.Groups[1].Value.Trim() : null;

        // ── 6. Parse ORDER BY ─────────────────────────────────────────────────
        Match orderMatch = Regex.Match(
            sql,
            @"ORDER\s+BY\s+(.+?)(?=\s+(?:LIMIT|$))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? orderBy = orderMatch.Success ? orderMatch.Groups[1].Value.Trim() : null;

        // ── 7. Parse LIMIT / TOP ──────────────────────────────────────────────
        int? limitVal = null;
        Match limitMatch = Regex.Match(sql, @"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase);
        if (limitMatch.Success)
            limitVal = int.Parse(limitMatch.Groups[1].Value);
        else
        {
            Match topMatch = Regex.Match(sql, @"\bTOP\s+(\d+)", RegexOptions.IgnoreCase);
            if (topMatch.Success)
                limitVal = int.Parse(topMatch.Groups[1].Value);
        }

        // ── 8. Spawn nodes ────────────────────────────────────────────────────
        const double baseX = 80;
        const double baseY = 120;
        const double colGap = 280;
        const double rowGap = 220;

        // Clear demo nodes — this is a fresh import
        _canvas.Connections.Clear();
        _canvas.Nodes.Clear();
        _canvas.UndoRedo.Clear();

        // Table source nodes
        var tableNodes = new List<NodeViewModel>();
        for (int i = 0; i < fromParts.Count; i++)
        {
            string tbl = fromParts[i].Table;
            var pos = new Point(baseX, baseY + i * rowGap);

            // Try to find in demo catalog for real columns
            var catalogEntry = CanvasViewModel.DemoCatalog.FirstOrDefault(t =>
                t.FullName.Equals(tbl, StringComparison.OrdinalIgnoreCase)
                || t.FullName.EndsWith("." + tbl, StringComparison.OrdinalIgnoreCase)
            );

            NodeViewModel tableNode =
                catalogEntry != default
                    ? new NodeViewModel(catalogEntry.FullName, catalogEntry.Cols, pos)
                    : new NodeViewModel(tbl, [], pos); // unknown table — no columns

            _canvas.Nodes.Add(tableNode);
            tableNodes.Add(tableNode);
            report.Add(
                new ImportReportItem(
                    fromParts[i].JoinType is not null
                        ? $"{fromParts[i].JoinType}: {tbl}"
                        : $"FROM: {tbl}",
                    ImportItemStatus.Imported
                )
            );
            imported++;
        }

        // ResultOutput node
        double resultY = baseY + (fromParts.Count - 1) * rowGap / 2.0;
        NodeViewModel result = new(
            NodeDefinitionRegistry.Get(NodeType.ResultOutput),
            new Point(baseX + colGap * 3, resultY)
        );
        _canvas.Nodes.Add(result);

        // Wire columns to ResultOutput
        NodeViewModel primaryNode = tableNodes[0];
        if (isStar)
        {
            // SELECT * — connect all output pins of primary table
            foreach (PinViewModel pin in primaryNode.OutputPins)
                SafeWire(primaryNode, pin.Name, result, "columns");
            report.Add(
                new ImportReportItem("SELECT *", ImportItemStatus.Imported, "All columns wired")
            );
            imported++;
        }
        else if (selectedCols.Count > 0)
        {
            int wired = 0;
            foreach ((string expr, string? alias) in selectedCols)
            {
                // Simple column reference: table.col or col
                string colName = expr.Split('.').Last().Trim();
                // Find the pin across all table nodes
                PinViewModel? pin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (pin is not null)
                {
                    SafeWire(pin.Owner, pin.Name, result, "columns");
                    wired++;
                }
                else
                {
                    // Expression or unknown column — note it
                    report.Add(
                        new ImportReportItem(
                            $"Column: {expr}",
                            ImportItemStatus.Skipped,
                            "Complex expression — wire manually"
                        )
                    );
                    skipped++;
                }
            }
            report.Add(
                new ImportReportItem(
                    $"SELECT ({wired}/{selectedCols.Count} columns)",
                    wired == selectedCols.Count
                        ? ImportItemStatus.Imported
                        : ImportItemStatus.Partial
                )
            );
            if (wired == selectedCols.Count)
                imported++;
            else
                partial++;
        }

        // WHERE clause
        if (whereClause is not null)
        {
            // Try simple equality: col = 'value' or col = value
            Match eqMatch = Regex.Match(
                whereClause,
                @"^(\w+(?:\.\w+)?)\s*(=|<>|!=|>|>=|<|<=)\s*(.+)$",
                RegexOptions.IgnoreCase
            );

            if (eqMatch.Success && !Regex.IsMatch(whereClause, @"\b(AND|OR)\b", RegexOptions.IgnoreCase))
            {
                string leftExpr = eqMatch.Groups[1].Value.Trim().Split('.').Last();
                string op = eqMatch.Groups[2].Value.Trim();
                string rightExpr = eqMatch.Groups[3].Value.Trim().Trim('\'', '"');

                NodeType compType = op switch
                {
                    "=" => NodeType.Equals,
                    "<>" or "!=" => NodeType.NotEquals,
                    ">" => NodeType.GreaterThan,
                    ">=" => NodeType.GreaterOrEqual,
                    "<" => NodeType.LessThan,
                    "<=" => NodeType.LessOrEqual,
                    _ => NodeType.Equals,
                };

                NodeViewModel comp = new(
                    NodeDefinitionRegistry.Get(compType),
                    new Point(baseX + colGap, baseY + fromParts.Count * rowGap)
                );
                comp.PinLiterals["right"] = rightExpr;
                _canvas.Nodes.Add(comp);

                NodeViewModel where = new(
                    NodeDefinitionRegistry.Get(NodeType.WhereOutput),
                    new Point(baseX + colGap * 2, baseY + fromParts.Count * rowGap)
                );
                _canvas.Nodes.Add(where);

                // Connect left pin of comparison from primary table column
                PinViewModel? leftPin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p =>
                        p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase)
                    );
                if (leftPin is not null)
                    SafeWire(leftPin.Owner, leftPin.Name, comp, "left");

                SafeWire(comp, "result", where, "condition");

                report.Add(
                    new ImportReportItem(
                        $"WHERE {leftExpr} {op} '{rightExpr}'",
                        ImportItemStatus.Imported
                    )
                );
                imported++;
            }
            else
            {
                // Complex WHERE — create a WhereOutput node and note the clause
                NodeViewModel where = new(
                    NodeDefinitionRegistry.Get(NodeType.WhereOutput),
                    new Point(baseX + colGap * 2, baseY + fromParts.Count * rowGap)
                );
                _canvas.Nodes.Add(where);

                report.Add(
                    new ImportReportItem(
                        $"WHERE {Truncate(whereClause, 40)}",
                        ImportItemStatus.Partial,
                        "Complex condition — connect manually"
                    )
                );
                partial++;
            }
        }

        // ORDER BY
        if (orderBy is not null)
        {
            report.Add(
                new ImportReportItem(
                    $"ORDER BY {Truncate(orderBy, 30)}",
                    ImportItemStatus.Skipped,
                    "No Sort node — add manually"
                )
            );
            skipped++;
        }

        // LIMIT / TOP
        if (limitVal.HasValue)
        {
            NodeViewModel top = new(
                NodeDefinitionRegistry.Get(NodeType.Top),
                new Point(baseX + colGap * 3, resultY - 120)
            );
            top.Parameters["count"] = limitVal.Value.ToString();
            _canvas.Nodes.Add(top);
            SafeWire(result, "output", top, "input");
            report.Add(new ImportReportItem($"LIMIT {limitVal}", ImportItemStatus.Imported));
            imported++;
        }

        if (isDistinct)
        {
            report.Add(
                new ImportReportItem(
                    "SELECT DISTINCT",
                    ImportItemStatus.Skipped,
                    "No Distinct node — deduplicate manually"
                )
            );
            skipped++;
        }

        return (imported, partial, skipped);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SafeWire(NodeViewModel from, string fromPin, NodeViewModel to, string toPin)
    {
        PinViewModel? fp =
            from.OutputPins.FirstOrDefault(p =>
                p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase)
            )
            ?? from.InputPins.FirstOrDefault(p =>
                p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase)
            );
        PinViewModel? tp =
            to.InputPins.FirstOrDefault(p =>
                p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase)
            )
            ?? to.OutputPins.FirstOrDefault(p =>
                p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase)
            );
        if (fp is null || tp is null)
            return;
        var conn = new ConnectionViewModel(fp, default, default) { ToPin = tp };
        fp.IsConnected = true;
        tp.IsConnected = true;
        _canvas.Connections.Add(conn);
    }

    private static string ExtractTableName(string part)
    {
        // "schema.table AS alias" or "schema.table alias" or just "table"
        string trimmed = part.Trim();
        Match m = Regex.Match(trimmed, @"^([\w.]+)(?:\s+(?:AS\s+)?\w+)?$", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : trimmed.Split(' ')[0];
    }

    private static List<string> SplitCommas(string s)
    {
        // Split on commas that are not inside parentheses
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(')
                depth++;
            else if (s[i] == ')')
                depth--;
            else if (s[i] == ',' && depth == 0)
            {
                parts.Add(s[start..i]);
                start = i + 1;
            }
        }
        parts.Add(s[start..]);
        return parts;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

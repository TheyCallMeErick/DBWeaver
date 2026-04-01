using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.SqlImport.Build;
using VisualSqlArchitect.UI.Services.SqlImport;
using VisualSqlArchitect.UI.ViewModels.UndoRedo;


namespace VisualSqlArchitect.UI.ViewModels.Canvas;

// ─── Conversion report item ───────────────────────────────────────────────────

public enum ImportItemStatus
{
    Imported,
    Partial,
    Skipped,
}

public sealed class ImportReportItem(
    string label,
    ImportItemStatus status,
    string? note = null,
    string? sourceNodeId = null)
{
    public string Label { get; } = label;
    public ImportItemStatus Status { get; } = status;
    public string? Note { get; } =
        status is ImportItemStatus.Partial or ImportItemStatus.Skipped
            ? (string.IsNullOrWhiteSpace(note) ? "Requires manual review." : note)
            : note;
    public string? SourceNodeId { get; } = sourceNodeId;
    public bool CanFocusNode => !string.IsNullOrWhiteSpace(SourceNodeId);

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
    private CancellationTokenSource? _importCts;
    private bool _cancelRequestedByUser;

    private bool _isVisible;
    private bool _isImporting;
    private string _sqlInput = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _hasReport;
    private int _reportImportedCount;
    private int _reportPartialCount;
    private int _reportSkippedCount;
    private double _lastParseDurationMs;
    private double _lastMapDurationMs;
    private double _lastBuildDurationMs;
    private double _lastTotalDurationMs;

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

    public int ReportImportedCount
    {
        get => _reportImportedCount;
        private set => Set(ref _reportImportedCount, value);
    }

    public int ReportPartialCount
    {
        get => _reportPartialCount;
        private set => Set(ref _reportPartialCount, value);
    }

    public int ReportSkippedCount
    {
        get => _reportSkippedCount;
        private set => Set(ref _reportSkippedCount, value);
    }

    public double LastParseDurationMs
    {
        get => _lastParseDurationMs;
        private set => Set(ref _lastParseDurationMs, value);
    }

    public double LastMapDurationMs
    {
        get => _lastMapDurationMs;
        private set => Set(ref _lastMapDurationMs, value);
    }

    public double LastBuildDurationMs
    {
        get => _lastBuildDurationMs;
        private set => Set(ref _lastBuildDurationMs, value);
    }

    public double LastTotalDurationMs
    {
        get => _lastTotalDurationMs;
        private set => Set(ref _lastTotalDurationMs, value);
    }

    /// <summary>
    /// Maximum accepted size (in characters) for SQL text import.
    /// Set to 0 or less to disable the limit.
    /// </summary>
    public int MaxSqlInputLength { get; set; } = AppConstants.DefaultMaxSqlInputLength;

    /// <summary>
    /// Maximum time allowed for an import execution.
    /// Set to zero or a negative value to disable timeout.
    /// </summary>
    public TimeSpan ImportTimeout { get; set; } = AppConstants.DefaultImportTimeout;

    /// <summary>
    /// Small async delay used to yield UI updates before import starts.
    /// </summary>
    public int ImportStartDelayMs { get; set; } = AppConstants.DefaultImportStartDelayMs;

    public SqlImportFeatureFlags FeatureFlags { get; } = new();

    public ObservableCollection<ImportReportItem> Report { get; } = [];

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Open()
    {
        SqlInput = string.Empty;
        Report.Clear();
        ResetReportTotals();
        HasReport = false;
        StatusMessage = string.Empty;
        IsVisible = true;
    }

    public void Close()
    {
        IsVisible = false;
    }

    public void CancelImport()
    {
        if (!IsImporting)
            return;

        _cancelRequestedByUser = true;
        _importCts?.Cancel();
    }

    public bool FocusReportItem(ImportReportItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.SourceNodeId))
            return false;

        NodeViewModel? node = _canvas.Nodes.FirstOrDefault(n =>
            n.Id.Equals(item.SourceNodeId, StringComparison.Ordinal));
        if (node is null)
            return false;

        _canvas.SelectNode(node);
        return true;
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SqlInput))
        {
            StatusMessage = "Paste a SELECT statement above, then click Import.";
            return;
        }

        if (IsImporting)
            return;

        ClearTelemetry();

        if (MaxSqlInputLength > 0 && SqlInput.Length > MaxSqlInputLength)
        {
            StatusMessage =
                $"SQL input is too large ({SqlInput.Length:N0} chars). Limit is {MaxSqlInputLength:N0}. Split the query or increase the import limit.";
            Report.Clear();
            ResetReportTotals();
            HasReport = false;
            return;
        }
        IsImporting = true;
        _cancelRequestedByUser = false;
        StatusMessage = "Parsing SQL…";
        Report.Clear();
        ResetReportTotals();
        HasReport = false;

        _importCts?.Cancel();
        _importCts?.Dispose();
        _importCts = new CancellationTokenSource();

        if (ImportTimeout > TimeSpan.Zero)
            _importCts.CancelAfter(ImportTimeout);

        CancellationToken token = _importCts.Token;

        // REGRESSION FIX: Capture canvas state before import for undo capability
        // Previously: SQL Import would clear canvas without any way to restore state
        // Now: Create restore command before import and register it to undo stack if successful
        var stateBeforeImport = new RestoreCanvasStateCommand(_canvas, "SQL Import");

        try
        {
            await Task.Delay(Math.Max(0, ImportStartDelayMs), token); // yield to update UI before heavy work

            string sqlToImport = SqlInput.Trim();
            if (FeatureFlags.UseAstParser)
            {
                var parser = new SqlParserService();
                SqlParseResult parseResult = parser.Parse(sqlToImport);
                if (!parseResult.Success)
                    throw new InvalidOperationException(parseResult.ToUserMessage());

                sqlToImport = parseResult.NormalizedSql ?? sqlToImport;
            }

            (int imported, int partial, int skipped, ImportTiming timing) = BuildGraph(sqlToImport, Report, token);
            ApplyTelemetry(timing);
            ApplyReportTotals(imported, partial, skipped);
            StatusMessage =
                $"Done — {imported} imported, {partial} partial, {skipped} skipped.";
            HasReport = true;

            // Capture post-import state so Redo can reapply import after Undo.
            stateBeforeImport.CaptureAfterState(_canvas);

            // Register restore command in undo stack to allow undoing the import
            // This lets users undo the import and go back to the pre-import state
            _canvas.UndoRedo.Execute(stateBeforeImport);

            if (imported + partial > 0)
                Close();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            StatusMessage =
                _cancelRequestedByUser
                    ? "Import cancelled by user."
                    : $"Import timed out after {ImportTimeout.TotalSeconds:0.#}s. Try a smaller query or increase timeout.";
            HasReport = false;
            Report.Clear();
            ResetReportTotals();

            // On cancellation/timeout, restore pre-import canvas state immediately
            stateBeforeImport.Execute(_canvas);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Parse error: {ex.Message}";
            ResetReportTotals();
            // On error, restore the pre-import canvas state immediately
            stateBeforeImport.Execute(_canvas);
        }
        finally
        {
            IsImporting = false;
            _importCts?.Dispose();
            _importCts = null;
        }
    }

    // ── Graph builder ─────────────────────────────────────────────────────────

    private (int imported, int partial, int skipped, ImportTiming timing) BuildGraph(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken ct
    )
    {
        int imported = 0,
            partial = 0,
            skipped = 0;

        var totalWatch = Stopwatch.StartNew();
        var parseWatch = Stopwatch.StartNew();

        ct.ThrowIfCancellationRequested();

        ValidateBasicSyntax(sql);

        // ── 1. Strip comments ────────────────────────────────────────────────
        sql = Regex.Replace(sql, @"--[^\n]*", " ");
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"\s+", " ").Trim();

        // ── 2. Detect unsupported constructs ─────────────────────────────────
        if (Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase))
        {
            if (TryRewriteSimpleCteQuery(sql, out string rewrittenSql, out int rewrittenCteCount))
            {
                sql = rewrittenSql;
                report.Add(
                    new ImportReportItem(
                        "CTE",
                        ImportItemStatus.Imported,
                        rewrittenCteCount == 1
                            ? "Single CTE rewritten to supported import shape."
                            : $"{rewrittenCteCount} chained CTEs rewritten to supported import shape."
                    )
                );
                imported++;
            }
        }

        bool hasSupportedWhereSubquery = Regex.IsMatch(
            sql,
            @"\bWHERE\s+(?:EXISTS\s*\(\s*SELECT|\w+(?:\.\w+)?\s+(?:NOT\s+)?IN\s*\(\s*SELECT|\w+(?:\.\w+)?\s*(?:=|<>|!=|>|>=|<|<=)\s*\(\s*SELECT)",
            RegexOptions.IgnoreCase
        );

        bool hasUnsupportedCteOrSubquery =
            Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase)
            || (
                Regex.IsMatch(sql, @"\(SELECT\b", RegexOptions.IgnoreCase)
                && !hasSupportedWhereSubquery
            );
        bool hasCorrelatedSubquery =
            Regex.IsMatch(sql, @"\b(EXISTS|IN)\s*\(\s*SELECT\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(sql, @"\b\w+\.\w+\s*=\s*\w+\.\w+\b", RegexOptions.IgnoreCase);
        HashSet<string> outerAliases = ExtractSourceAliases(fromSql: Regex.Match(
            sql,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|GROUP\s+BY)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        ).Success
            ? Regex.Match(
                sql,
                @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|GROUP\s+BY)|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).Groups[1].Value.Trim()
            : string.Empty);

        if (hasUnsupportedCteOrSubquery)
        {
            if (Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase))
            {
                foreach (string cteIssue in AnalyzeCteNameIssues(sql))
                {
                    report.Add(
                        new ImportReportItem(
                            "CTE name diagnostics",
                            ImportItemStatus.Partial,
                            cteIssue
                        )
                    );
                    partial++;
                }
            }

            if (hasCorrelatedSubquery)
            {
                string correlatedFields = DescribeCorrelatedOuterReferences(sql, outerAliases);
                report.Add(
                    new ImportReportItem(
                        "Correlated sub-query",
                        ImportItemStatus.Partial,
                        string.IsNullOrWhiteSpace(correlatedFields)
                            ? "Correlated sub-query is not yet supported and falls back to a safe partial import path."
                            : $"Correlated sub-query is not yet supported and falls back to a safe partial import path. External refs: {correlatedFields}."
                    )
                );
                partial++;
            }

            report.Add(
                new ImportReportItem(
                    "CTE / sub-query",
                    ImportItemStatus.Skipped,
                    "CTEs and sub-queries are not supported"
                )
            );
            skipped++;

            report.Add(
                new ImportReportItem(
                    "Raw fallback",
                    ImportItemStatus.Skipped,
                    "Raw fallback is disabled for CTE/sub-query blocks to avoid unsafe or ambiguous SQL materialization."
                )
            );
            skipped++;

            // Keep import resilient for valid but unsupported SQL:
            // do not continue with regex-based projection/from parsing that can misinterpret CTE/subquery syntax.
            parseWatch.Stop();
            totalWatch.Stop();

            return (
                imported,
                partial,
                skipped,
                new ImportTiming(parseWatch.Elapsed, TimeSpan.Zero, TimeSpan.Zero, totalWatch.Elapsed)
            );
        }

        if (Regex.IsMatch(sql, @"\bUNION\b", RegexOptions.IgnoreCase))
        {
            report.Add(
                new ImportReportItem("UNION", ImportItemStatus.Skipped, "UNION is not supported")
            );
            skipped++;
        }

        parseWatch.Stop();
        var mapWatch = Stopwatch.StartNew();

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
                ct.ThrowIfCancellationRequested();
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
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|GROUP\s+BY)|$)",
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
            @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+(\w+(?:\.\w+)?)(?:\s+(?:AS\s+)?\w+)?\s+ON\s+(.+?)(?=\s+(?:INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+JOIN|JOIN)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        foreach (Match jm in joinMatches)
        {
            ct.ThrowIfCancellationRequested();
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
            @"WHERE\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP|GROUP\s+BY)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? whereClause = whereMatch.Success ? whereMatch.Groups[1].Value.Trim() : null;

        // ── 6. Parse ORDER BY ─────────────────────────────────────────────────
        Match orderMatch = Regex.Match(
            sql,
            @"ORDER\s+BY\s+(.+?)(?=\s+LIMIT|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? orderBy = orderMatch.Success ? orderMatch.Groups[1].Value.Trim() : null;

        // ── 7. Parse GROUP BY ───────────────────────────────────────────────
        Match groupMatch = Regex.Match(
            sql,
            @"GROUP\s+BY\s+(.+?)(?=\s+(?:HAVING|ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? groupBy = groupMatch.Success ? groupMatch.Groups[1].Value.Trim() : null;

        // ── 8. Parse HAVING ─────────────────────────────────────────────────
        Match havingMatch = Regex.Match(
            sql,
            @"HAVING\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? havingClause = havingMatch.Success ? havingMatch.Groups[1].Value.Trim() : null;

        // ── 9. Parse LIMIT / TOP ──────────────────────────────────────────────
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

        mapWatch.Stop();
        var buildWatch = Stopwatch.StartNew();

        // ── 8. Spawn nodes ────────────────────────────────────────────────────
        const double baseX = 80;
        const double baseY = 120;
        const double colGap = 280;
        const double rowGap = 220;

        var coreBuilder = new ImportModelToCanvasBuilder(_canvas);
        var coreInput = new ImportBuildInput(
            fromParts.Select(p => new ImportFromPart(p.Table, p.JoinType, p.OnClause)).ToList(),
            selectedCols.Select(c => new ImportSelectTerm(c.Expr, c.Alias)).ToList(),
            isStar,
            new ImportLayout(baseX, baseY, colGap, rowGap)
        );
        ImportBuildContext coreContext = coreBuilder.BuildCore(coreInput, report, ct);

        imported += coreContext.Imported;
        partial += coreContext.Partial;
        skipped += coreContext.Skipped;

        var tableNodes = coreContext.TableNodes;
        NodeViewModel result = coreContext.ResultNode;
        var projectedAliases = coreContext.ProjectedAliases;
        double resultY = coreContext.ResultY;

        // WHERE clause
        if (whereClause is not null)
        {
            Match existsMatch = Regex.Match(
                whereClause,
                @"^EXISTS\s*\((SELECT.+)\)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );
            if (existsMatch.Success)
            {
                string subquerySql = existsMatch.Groups[1].Value.Trim();
                string correlatedFields = DescribeCorrelatedOuterReferences(subquerySql, outerAliases);
                NodeViewModel existsNode = new(
                    NodeDefinitionRegistry.Get(NodeType.SubqueryExists),
                    new Point(baseX + colGap * 2, baseY + fromParts.Count * rowGap)
                );
                existsNode.Parameters["query"] = subquerySql;
                _canvas.Nodes.Add(existsNode);
                SafeWire(existsNode, "result", result, "where");

                report.Add(
                    new ImportReportItem(
                        "WHERE EXISTS(sub-query)",
                        ImportItemStatus.Imported,
                        sourceNodeId: existsNode.Id
                    )
                );
                imported++;
                if (!string.IsNullOrWhiteSpace(correlatedFields))
                {
                    report.Add(
                        new ImportReportItem(
                            "Correlation fields",
                            ImportItemStatus.Imported,
                            $"External references: {correlatedFields}",
                            existsNode.Id
                        )
                    );
                    imported++;
                }
                goto WHERE_HANDLED;
            }

            Match inSubqueryMatch = Regex.Match(
                whereClause,
                @"^(\w+(?:\.\w+)?)\s+(NOT\s+)?IN\s*\((SELECT.+)\)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );
            if (inSubqueryMatch.Success)
            {
                string leftExpr = inSubqueryMatch.Groups[1].Value.Trim().Split('.').Last();
                bool negate = inSubqueryMatch.Groups[2].Success;
                string subquerySql = inSubqueryMatch.Groups[3].Value.Trim();
                string correlatedFields = DescribeCorrelatedOuterReferences(subquerySql, outerAliases);

                NodeViewModel inNode = new(
                    NodeDefinitionRegistry.Get(NodeType.SubqueryIn),
                    new Point(baseX + colGap * 2, baseY + fromParts.Count * rowGap)
                );
                inNode.Parameters["query"] = subquerySql;
                inNode.Parameters["negate"] = negate ? "true" : "false";
                _canvas.Nodes.Add(inNode);

                PinViewModel? valuePin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
                if (valuePin is not null)
                    SafeWire(valuePin.Owner, valuePin.Name, inNode, "value");

                SafeWire(inNode, "result", result, "where");

                report.Add(
                    new ImportReportItem(
                        negate ? "WHERE value NOT IN(sub-query)" : "WHERE value IN(sub-query)",
                        ImportItemStatus.Imported,
                        sourceNodeId: inNode.Id
                    )
                );
                imported++;
                if (!string.IsNullOrWhiteSpace(correlatedFields))
                {
                    report.Add(
                        new ImportReportItem(
                            "Correlation fields",
                            ImportItemStatus.Imported,
                            $"External references: {correlatedFields}",
                            inNode.Id
                        )
                    );
                    imported++;
                }
                goto WHERE_HANDLED;
            }

            Match scalarSubqueryMatch = Regex.Match(
                whereClause,
                @"^(\w+(?:\.\w+)?)\s*(=|<>|!=|>|>=|<|<=)\s*\((SELECT.+)\)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );
            if (scalarSubqueryMatch.Success)
            {
                string leftExpr = scalarSubqueryMatch.Groups[1].Value.Trim().Split('.').Last();
                string op = scalarSubqueryMatch.Groups[2].Value.Trim();
                string subquerySql = scalarSubqueryMatch.Groups[3].Value.Trim();
                string correlatedFields = DescribeCorrelatedOuterReferences(subquerySql, outerAliases);

                NodeViewModel scalarNode = new(
                    NodeDefinitionRegistry.Get(NodeType.SubqueryScalar),
                    new Point(baseX + colGap * 2, baseY + fromParts.Count * rowGap)
                );
                scalarNode.Parameters["query"] = subquerySql;
                scalarNode.Parameters["operator"] = op == "!=" ? "<>" : op;
                _canvas.Nodes.Add(scalarNode);

                PinViewModel? leftPin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
                if (leftPin is not null)
                    SafeWire(leftPin.Owner, leftPin.Name, scalarNode, "left");

                SafeWire(scalarNode, "result", result, "where");

                report.Add(
                    new ImportReportItem(
                        "WHERE value op (scalar sub-query)",
                        ImportItemStatus.Imported,
                        sourceNodeId: scalarNode.Id
                    )
                );
                imported++;
                if (!string.IsNullOrWhiteSpace(correlatedFields))
                {
                    report.Add(
                        new ImportReportItem(
                            "Correlation fields",
                            ImportItemStatus.Imported,
                            $"External references: {correlatedFields}",
                            scalarNode.Id
                        )
                    );
                    imported++;
                }
                goto WHERE_HANDLED;
            }

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
                SafeWire(where, "result", result, "where");

                report.Add(
                    new ImportReportItem(
                        $"WHERE {leftExpr} {op} '{rightExpr}'",
                        ImportItemStatus.Imported,
                        sourceNodeId: where.Id
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
                        "Complex condition — connect manually",
                        where.Id
                    )
                );
                partial++;
            }

        WHERE_HANDLED:;
        }

        // ORDER BY
        if (orderBy is not null)
        {
            string[] terms = orderBy
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var importedOrderTerms = new List<string>();
            int importedTerms = 0;

            foreach (string term in terms)
            {
                ct.ThrowIfCancellationRequested();

                Match termMatch = Regex.Match(
                    term,
                    @"^(\w+(?:\.\w+)?)(?:\s+(ASC|DESC))?$",
                    RegexOptions.IgnoreCase
                );

                if (!termMatch.Success)
                    continue;

                string expr = termMatch.Groups[1].Value.Trim();
                string colName = expr.Split('.').Last();
                bool desc =
                    termMatch.Groups[2].Success
                    && termMatch.Groups[2].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase);

                PinViewModel? orderPin = null;
                if (projectedAliases.TryGetValue(colName, out PinViewModel? aliasedPin))
                    orderPin = aliasedPin;
                else
                {
                    orderPin = tableNodes
                        .SelectMany(n => n.OutputPins)
                        .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                }

                if (orderPin is null)
                    continue;

                importedOrderTerms.Add(
                    string.Join(
                        '|',
                        orderPin.Owner.Id,
                        orderPin.Name,
                        desc ? "DESC" : "ASC"
                    )
                );
                importedTerms++;
            }

            if (importedOrderTerms.Count > 0)
            {
                result.Parameters["import_order_terms"] = string.Join(';', importedOrderTerms);
            }

            if (importedTerms == terms.Length)
            {
                report.Add(
                    new ImportReportItem(
                        $"ORDER BY {Truncate(orderBy, 30)}",
                        ImportItemStatus.Imported,
                        sourceNodeId: result.Id
                    )
                );
                imported++;
            }
            else if (importedTerms > 0)
            {
                report.Add(
                    new ImportReportItem(
                        $"ORDER BY {Truncate(orderBy, 30)}",
                        ImportItemStatus.Partial,
                        "Some sort terms could not be mapped and were skipped",
                        result.Id
                    )
                );
                partial++;
            }
            else
            {
                report.Add(
                    new ImportReportItem(
                        $"ORDER BY {Truncate(orderBy, 30)}",
                        ImportItemStatus.Skipped,
                        "Unsupported sort expression — add manually"
                    )
                );
                skipped++;
            }
        }

        // GROUP BY
        if (groupBy is not null)
        {
            string[] terms = groupBy
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var importedGroupTerms = new List<string>();
            int importedTerms = 0;

            foreach (string term in terms)
            {
                ct.ThrowIfCancellationRequested();

                Match termMatch = Regex.Match(
                    term,
                    @"^(\w+(?:\.\w+)?)$",
                    RegexOptions.IgnoreCase
                );

                if (!termMatch.Success)
                    continue;

                string expr = termMatch.Groups[1].Value.Trim();
                string colName = expr.Split('.').Last();

                PinViewModel? groupPin = null;
                if (projectedAliases.TryGetValue(colName, out PinViewModel? aliasedPin))
                    groupPin = aliasedPin;
                else
                {
                    groupPin = tableNodes
                        .SelectMany(n => n.OutputPins)
                        .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                }

                if (groupPin is null)
                    continue;

                importedGroupTerms.Add(string.Join('|', groupPin.Owner.Id, groupPin.Name));
                importedTerms++;
            }

            if (importedGroupTerms.Count > 0)
                result.Parameters["import_group_terms"] = string.Join(';', importedGroupTerms);

            if (importedTerms == terms.Length)
            {
                report.Add(
                    new ImportReportItem(
                        $"GROUP BY {Truncate(groupBy, 30)}",
                        ImportItemStatus.Imported,
                        sourceNodeId: result.Id
                    )
                );
                imported++;
            }
            else if (importedTerms > 0)
            {
                report.Add(
                    new ImportReportItem(
                        $"GROUP BY {Truncate(groupBy, 30)}",
                        ImportItemStatus.Partial,
                        "Some grouping terms could not be mapped and were skipped",
                        result.Id
                    )
                );
                partial++;
            }
            else
            {
                report.Add(
                    new ImportReportItem(
                        $"GROUP BY {Truncate(groupBy, 30)}",
                        ImportItemStatus.Skipped,
                        "Unsupported grouping expression — add manually"
                    )
                );
                skipped++;
            }

            var groupedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string term in terms)
            {
                Match groupedTermMatch = Regex.Match(term, @"^(\w+(?:\.\w+)?)$", RegexOptions.IgnoreCase);
                if (!groupedTermMatch.Success)
                    continue;

                string groupedExpr = groupedTermMatch.Groups[1].Value.Trim();
                groupedTerms.Add(groupedExpr);
                groupedTerms.Add(groupedExpr.Split('.').Last());
            }

            foreach ((string expr, string? alias) in selectedCols)
            {
                if (LooksLikeAggregateExpression(expr))
                    continue;

                string exprTrimmed = expr.Trim();
                string exprShort = exprTrimmed.Split('.').Last();
                bool isGrouped = groupedTerms.Contains(exprTrimmed)
                    || groupedTerms.Contains(exprShort)
                    || (!string.IsNullOrWhiteSpace(alias) && groupedTerms.Contains(alias.Trim()));

                if (isGrouped)
                    continue;

                report.Add(
                    new ImportReportItem(
                        $"GROUP BY conflict: {Truncate(exprTrimmed, 40)}",
                        ImportItemStatus.Partial,
                        "Selected column is neither grouped nor aggregated"
                    )
                );
                partial++;
            }
        }

        // HAVING
        if (havingClause is not null)
        {
            Match countHavingMatch = Regex.Match(
                havingClause,
                @"^(COUNT\s*\(\s*(?:\*|1)\s*\))\s*(=|<>|!=|>|>=|<|<=)\s*(.+)$",
                RegexOptions.IgnoreCase
            );

            if (countHavingMatch.Success)
            {
                string op = countHavingMatch.Groups[2].Value.Trim();
                string rightExpr = countHavingMatch.Groups[3].Value.Trim().Trim('\'', '"');

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

                NodeViewModel countNode = new(
                    NodeDefinitionRegistry.Get(NodeType.CountStar),
                    new Point(baseX + colGap, baseY + fromParts.Count * rowGap + 80)
                );
                _canvas.Nodes.Add(countNode);

                NodeViewModel comp = new(
                    NodeDefinitionRegistry.Get(compType),
                    new Point(baseX + colGap * 2, baseY + fromParts.Count * rowGap + 80)
                );
                comp.PinLiterals["right"] = rightExpr;
                _canvas.Nodes.Add(comp);

                SafeWire(countNode, "count", comp, "left");
                SafeWire(comp, "result", result, "having");

                report.Add(
                    new ImportReportItem(
                        $"HAVING COUNT(*) {op} {rightExpr}",
                        ImportItemStatus.Imported,
                        sourceNodeId: comp.Id
                    )
                );
                imported++;
            }
            else
            {
                report.Add(
                    new ImportReportItem(
                        $"HAVING {Truncate(havingClause, 40)}",
                        ImportItemStatus.Partial,
                        "Complex HAVING expression — connect predicate manually",
                        result.Id
                    )
                );
                partial++;
            }
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
            report.Add(
                new ImportReportItem($"LIMIT {limitVal}", ImportItemStatus.Imported, sourceNodeId: top.Id)
            );
            imported++;
        }

        if (isDistinct)
        {
            result.Parameters["distinct"] = "true";
            report.Add(
                new ImportReportItem(
                    "SELECT DISTINCT",
                    ImportItemStatus.Imported,
                    "ResultOutput distinct flag enabled",
                    result.Id
                )
            );
            imported++;
        }

        buildWatch.Stop();
        totalWatch.Stop();

        return (
            imported,
            partial,
            skipped,
            new ImportTiming(parseWatch.Elapsed, mapWatch.Elapsed, buildWatch.Elapsed, totalWatch.Elapsed)
        );
    }

    private void ClearTelemetry()
    {
        LastParseDurationMs = 0;
        LastMapDurationMs = 0;
        LastBuildDurationMs = 0;
        LastTotalDurationMs = 0;
    }

    private void ApplyTelemetry(ImportTiming timing)
    {
        LastParseDurationMs = timing.Parse.TotalMilliseconds;
        LastMapDurationMs = timing.Map.TotalMilliseconds;
        LastBuildDurationMs = timing.Build.TotalMilliseconds;
        LastTotalDurationMs = timing.Total.TotalMilliseconds;
    }

    private void ApplyReportTotals(int imported, int partial, int skipped)
    {
        ReportImportedCount = imported;
        ReportPartialCount = partial;
        ReportSkippedCount = skipped;
    }

    private void ResetReportTotals() => ApplyReportTotals(0, 0, 0);

    private readonly record struct ImportTiming(
        TimeSpan Parse,
        TimeSpan Map,
        TimeSpan Build,
        TimeSpan Total
    );

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

        if (!tp.CanAccept(fp))
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

    private static string NormalizeJoinType(string rawJoinType)
    {
        string normalized = rawJoinType.ToUpperInvariant();
        if (normalized.Contains("LEFT", StringComparison.Ordinal))
            return "LEFT";
        if (normalized.Contains("RIGHT", StringComparison.Ordinal))
            return "RIGHT";
        if (normalized.Contains("FULL", StringComparison.Ordinal))
            return "FULL";
        if (normalized.Contains("CROSS", StringComparison.Ordinal))
            return "CROSS";
        return "INNER";
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

    private static void ValidateBasicSyntax(string sql)
    {
        if (TryFindUnterminatedSingleQuote(sql, out int quoteIndex))
        {
            (int line, int column) = GetLineAndColumn(sql, quoteIndex);
            throw new InvalidOperationException(
                $"Syntax error at line {line}, column {column}: unterminated string literal."
            );
        }

        if (TryFindUnmatchedParenthesis(sql, out int parenIndex, out bool missingClosing))
        {
            (int line, int column) = GetLineAndColumn(sql, parenIndex);
            string detail = missingClosing ? "missing closing ')'" : "unexpected ')'";
            throw new InvalidOperationException(
                $"Syntax error at line {line}, column {column}: {detail}."
            );
        }
    }

    private static bool TryFindUnterminatedSingleQuote(string sql, out int index)
    {
        bool inQuote = false;
        index = -1;

        for (int i = 0; i < sql.Length; i++)
        {
            if (sql[i] != '\'')
                continue;

            // Escaped quote in SQL string literal: ''
            if (inQuote && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                i++;
                continue;
            }

            inQuote = !inQuote;
            if (inQuote)
                index = i;
        }

        return inQuote;
    }

    private static HashSet<string> ExtractSourceAliases(string fromSql)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(fromSql))
            return aliases;

        void AddMatchAliases(Match m)
        {
            string source = m.Groups[1].Value.Trim();
            string explicitAlias = m.Groups[2].Success ? m.Groups[2].Value.Trim() : string.Empty;
            string implicitAlias = m.Groups[3].Success ? m.Groups[3].Value.Trim() : string.Empty;

            if (!string.IsNullOrWhiteSpace(explicitAlias))
                aliases.Add(explicitAlias);
            if (!string.IsNullOrWhiteSpace(implicitAlias))
                aliases.Add(implicitAlias);

            string shortName = source.Split('.').Last();
            if (!string.IsNullOrWhiteSpace(shortName))
                aliases.Add(shortName);
        }

        Match primary = Regex.Match(
            fromSql,
            @"^([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+([A-Za-z_][A-Za-z0-9_]*)|([A-Za-z_][A-Za-z0-9_]*))?",
            RegexOptions.IgnoreCase
        );
        if (primary.Success)
            AddMatchAliases(primary);

        MatchCollection joins = Regex.Matches(
            fromSql,
            @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+([A-Za-z_][A-Za-z0-9_]*)|([A-Za-z_][A-Za-z0-9_]*))?",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        foreach (Match jm in joins)
            AddMatchAliases(jm);

        return aliases;
    }

    private static string DescribeCorrelatedOuterReferences(string sql, HashSet<string> outerAliases)
    {
        if (outerAliases.Count == 0 || string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MatchCollection qualifiedRefs = Regex.Matches(sql, @"\b([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\b");
        foreach (Match match in qualifiedRefs)
        {
            string alias = match.Groups[1].Value;
            string col = match.Groups[2].Value;
            if (outerAliases.Contains(alias))
                refs.Add($"{alias}.{col}");
        }

        return string.Join(", ", refs.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> AnalyzeCteNameIssues(string sql)
    {
        var issues = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        MatchCollection cteMatches = Regex.Matches(
            sql,
            @"(?:\bWITH\b|,)\s*([^\s,()]+)\s+AS\s*\(",
            RegexOptions.IgnoreCase
        );

        foreach (Match m in cteMatches)
        {
            string rawName = m.Groups[1].Value.Trim();
            if (!Regex.IsMatch(rawName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                issues.Add($"Invalid CTE name '{rawName}'. Use letters, digits, and underscore; first char must be a letter or underscore.");
                continue;
            }

            if (!seen.Add(rawName))
            {
                issues.Add($"Duplicate CTE name '{rawName}'. CTE names must be unique in a WITH block.");
            }
        }

        return issues;
    }

    private static bool TryRewriteSimpleCteQuery(string sql, out string rewrittenSql, out int cteCount)
    {
        rewrittenSql = sql;
        cteCount = 0;

        if (!Regex.IsMatch(sql, @"^\s*WITH\b", RegexOptions.IgnoreCase))
            return false;

        if (!TryExtractCteDefinitions(sql, out var definitions, out string mainQuery))
            return false;

        if (AnalyzeCteNameIssues(sql).Any())
            return false;

        var resolvedSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string body) in definitions)
        {
            if (!TryExtractSimpleSelectSource(body, out string sourceName))
                return false;

            if (resolvedSources.TryGetValue(sourceName, out string? chainedSource))
                resolvedSources[name] = chainedSource;
            else
                resolvedSources[name] = sourceName;
        }

        string rewrittenMain = Regex.Replace(
            mainQuery,
            @"\b(FROM|JOIN)\s+([A-Za-z_][A-Za-z0-9_]*)\b",
            m =>
            {
                string keyword = m.Groups[1].Value;
                string source = m.Groups[2].Value;
                return resolvedSources.TryGetValue(source, out string? resolved)
                    ? $"{keyword} {resolved}"
                    : m.Value;
            },
            RegexOptions.IgnoreCase
        );

        cteCount = definitions.Count;
        rewrittenSql = rewrittenMain;
        return true;
    }

    private static bool TryExtractCteDefinitions(
        string sql,
        out List<(string name, string body)> definitions,
        out string mainQuery
    )
    {
        definitions = new List<(string name, string body)>();
        mainQuery = sql;

        int index = 0;
        while (index < sql.Length && char.IsWhiteSpace(sql[index]))
            index++;

        if (!sql.AsSpan(index).StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            return false;

        index += 4;
        while (index < sql.Length)
        {
            while (index < sql.Length && (char.IsWhiteSpace(sql[index]) || sql[index] == ','))
                index++;
            if (index >= sql.Length)
                return false;

            int nameStart = index;
            if (!IsIdentifierStart(sql[index]))
                return false;

            index++;
            while (index < sql.Length && IsIdentifierPart(sql[index]))
                index++;

            string name = sql[nameStart..index];

            while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                index++;

            if (!sql.AsSpan(index).StartsWith("AS", StringComparison.OrdinalIgnoreCase))
                return false;
            index += 2;

            while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                index++;
            if (index >= sql.Length || sql[index] != '(')
                return false;

            index++; // skip opening paren
            int bodyStart = index;
            int depth = 1;
            while (index < sql.Length && depth > 0)
            {
                if (sql[index] == '(')
                    depth++;
                else if (sql[index] == ')')
                    depth--;
                index++;
            }

            if (depth != 0)
                return false;

            int bodyEnd = index - 1;
            string body = sql[bodyStart..bodyEnd].Trim();
            definitions.Add((name, body));

            while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                index++;

            if (index < sql.Length && sql[index] == ',')
            {
                index++;
                continue;
            }

            mainQuery = sql[index..].Trim();
            return Regex.IsMatch(mainQuery, @"^SELECT\b", RegexOptions.IgnoreCase) && definitions.Count > 0;
        }

        return false;
    }

    private static bool TryExtractSimpleSelectSource(string cteBody, out string sourceName)
    {
        sourceName = string.Empty;

        if (Regex.IsMatch(cteBody, @"\b(WHERE|GROUP\s+BY|HAVING|ORDER\s+BY|JOIN|UNION)\b", RegexOptions.IgnoreCase))
            return false;

        Match m = Regex.Match(
            cteBody,
            @"^\s*SELECT\s+.+?\s+FROM\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        if (!m.Success)
            return false;

        sourceName = m.Groups[1].Value.Trim();
        return !string.IsNullOrWhiteSpace(sourceName);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool TryFindUnmatchedParenthesis(string sql, out int index, out bool missingClosing)
    {
        var stack = new Stack<int>();
        bool inQuote = false;
        index = -1;
        missingClosing = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char ch = sql[i];

            if (ch == '\'')
            {
                if (inQuote && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                inQuote = !inQuote;
                continue;
            }

            if (inQuote)
                continue;

            if (ch == '(')
            {
                stack.Push(i);
                continue;
            }

            if (ch == ')')
            {
                if (stack.Count == 0)
                {
                    index = i;
                    missingClosing = false;
                    return true;
                }

                stack.Pop();
            }
        }

        if (stack.Count > 0)
        {
            index = stack.Peek();
            missingClosing = true;
            return true;
        }

        return false;
    }

    private static (int line, int column) GetLineAndColumn(string text, int index)
    {
        int line = 1;
        int lineStart = 0;

        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        int column = Math.Max(1, index - lineStart + 1);
        return (line, column);
    }

    private static bool LooksLikeAggregateExpression(string expr) =>
        Regex.IsMatch(
            expr,
            @"^\s*(COUNT|SUM|AVG|MIN|MAX|STRING_AGG|ARRAY_AGG|JSON_AGG)\s*\(",
            RegexOptions.IgnoreCase
        );

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

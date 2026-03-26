using System.Collections.ObjectModel;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect.UI.ViewModels;

// ─── Token kinds for syntax-highlighted SQL display ───────────────────────────

public enum SqlTokenKind
{
    Keyword,    // SELECT, FROM, WHERE, JOIN, AS, ON, AND, OR …
    Identifier, // table.column, alias names
    Literal,    // '…', numbers
    Operator,   // =, <>, >, BETWEEN, LIKE …
    Punctuation,// ( ) , ;
    Function,   // UPPER(, CAST(, JSON_VALUE(…
    Comment,    // -- …
    Plain       // whitespace, unknown
}

public sealed record SqlToken(string Text, SqlTokenKind Kind);

// ─── Live SQL bar view model ──────────────────────────────────────────────────

/// <summary>
/// Maintains a real-time SQL preview that updates every time the canvas graph
/// changes (node added/removed, pin connected/disconnected, parameter edited).
///
/// The VM walks <see cref="CanvasViewModel"/> collections, compiles the graph
/// via <see cref="QueryGeneratorService"/>, and exposes:
///   • <see cref="RawSql"/>     — plain string for copy/paste
///   • <see cref="Tokens"/>     — syntax-highlighted token list for the UI
///   • <see cref="IsValid"/>    — false when the graph has errors
///   • <see cref="ErrorHints"/> — per-error messages for the validation panel
/// </summary>
public sealed class LiveSqlBarViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;

    private string  _rawSql           = string.Empty;
    private bool    _isValid          = true;
    private bool    _isCompiling;
    private string? _compileError;
    private bool    _isMutatingCommand;
    private DatabaseProvider _provider = DatabaseProvider.Postgres;

    // Mutating SQL keywords that must be blocked in Safe Preview Mode
    private static readonly string[] MutatingKeywords =
        ["INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE", "CREATE", "REPLACE", "MERGE"];

    // ── Observable collections ────────────────────────────────────────────────

    public ObservableCollection<SqlToken>    Tokens      { get; } = [];
    public ObservableCollection<string>      ErrorHints  { get; } = [];
    public ObservableCollection<GuardIssue>  GuardIssues { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string RawSql
    {
        get => _rawSql;
        private set { Set(ref _rawSql, value); _canvas.UpdateQueryText(value); }
    }

    public bool IsValid
    {
        get => _isValid;
        private set => Set(ref _isValid, value);
    }

    public bool IsCompiling
    {
        get => _isCompiling;
        private set => Set(ref _isCompiling, value);
    }

    public string? CompileError
    {
        get => _compileError;
        private set => Set(ref _compileError, value);
    }

    public DatabaseProvider Provider
    {
        get => _provider;
        set { Set(ref _provider, value); Recompile(); }
    }

    public string ProviderLabel => _provider switch
    {
        DatabaseProvider.SqlServer => "SQL Server",
        DatabaseProvider.MySql     => "MySQL",
        _                          => "PostgreSQL"
    };

    public bool HasSql => !string.IsNullOrWhiteSpace(RawSql);

    public bool IsMutatingCommand
    {
        get => _isMutatingCommand;
        private set => Set(ref _isMutatingCommand, value);
    }

    public string? BlockedReason => IsMutatingCommand
        ? "Blocked in Safe Preview Mode — SQL contains a data-mutating command (INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE)"
        : null;

    public bool HasGuardWarning => GuardIssues.Count > 0;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LiveSqlBarViewModel(CanvasViewModel canvas)
    {
        _canvas = canvas;

        // Subscribe to all relevant changes
        canvas.Nodes.CollectionChanged       += (_, _) => ScheduleRecompile();
        canvas.Connections.CollectionChanged += (_, _) => ScheduleRecompile();

        // React to node property changes (parameters, alias, position)
        canvas.Nodes.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is null) return;
            foreach (NodeViewModel vm in e.NewItems)
                vm.PropertyChanged += (_, _) => ScheduleRecompile();
        };

        Recompile();
    }

    // ── Debounced recompile ───────────────────────────────────────────────────

    private CancellationTokenSource? _debounce;

    private void ScheduleRecompile()
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        // 120ms debounce — avoids recompiling on every intermediate drag step
        Task.Delay(120, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                Avalonia.Threading.Dispatcher.UIThread.Post(Recompile);
        }, TaskScheduler.Default);
    }

    // ── Compilation ───────────────────────────────────────────────────────────

    public void Recompile()
    {
        ErrorHints.Clear();
        GuardIssues.Clear();
        IsCompiling = true;

        try
        {
            var (sql, errors) = BuildSql();

            RawSql       = sql;
            IsValid      = errors.Count == 0;
            CompileError = errors.Count > 0 ? errors[0] : null;

            // Detect mutating commands — block preview execution
            IsMutatingCommand = IsMutating(sql);
            RaisePropertyChanged(nameof(BlockedReason));

            // Run guardrails (only when not a mutating command — those are already blocked)
            if (!IsMutatingCommand)
            {
                foreach (var issue in QueryGuardrails.Check(sql))
                    GuardIssues.Add(issue);
            }
            RaisePropertyChanged(nameof(HasGuardWarning));

            foreach (var err in errors) ErrorHints.Add(err);

            TokenizeSql(sql);
        }
        catch (Exception ex)
        {
            RawSql            = string.Empty;
            IsValid           = false;
            CompileError      = ex.Message;
            IsMutatingCommand = false;
            ErrorHints.Add($"Compile error: {ex.Message}");
            Tokens.Clear();
        }
        finally
        {
            IsCompiling = false;
            RaisePropertyChanged(nameof(HasSql));
            RaisePropertyChanged(nameof(ProviderLabel));
            RaisePropertyChanged(nameof(BlockedReason));
            RaisePropertyChanged(nameof(HasGuardWarning));
        }
    }

    // ── Graph → SQL translation ───────────────────────────────────────────────

    private (string Sql, List<string> Errors) BuildSql()
    {
        var errors = new List<string>();

        // Require at least one TableSource node
        var tableNodes = _canvas.Nodes
            .Where(n => n.Type == NodeType.TableSource)
            .ToList();

        if (tableNodes.Count == 0)
        {
            // No tables on canvas yet — show placeholder
            return ("-- Add a table node to start building your query", errors);
        }

        // Build NodeGraph from the canvas state
        var graph = BuildNodeGraph();

        // Determine FROM table (first table source added)
        var fromTable = tableNodes[0].Subtitle ?? tableNodes[0].Title;

        // Build JOIN definitions from connections between TableSource nodes
        var joins = BuildJoins(tableNodes);

        try
        {
            var svc    = QueryGeneratorService.Create(_provider);
            var result = svc.Generate(fromTable, graph, joins);
            return (result.Sql, errors);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            // Fall back to a manually-built SQL string
            return (FallbackSql(tableNodes, joins), errors);
        }
    }

    private NodeGraph BuildNodeGraph()
    {
        // Convert CanvasViewModel collections to NodeGraph DTOs
        var nodes = _canvas.Nodes.Select(n => new VisualSqlArchitect.Nodes.NodeInstance(
            Id:            n.Id,
            Type:          n.Type,
            PinLiterals:   n.PinLiterals,
            Parameters:    n.Parameters,
            Alias:         n.Alias,
            TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
            ColumnPins:    n.Type == NodeType.TableSource
                ? n.OutputPins.ToDictionary(p => p.Name, p => p.Name)
                : null
        )).ToList();

        var connections = _canvas.Connections
            .Where(c => c.ToPin is not null)
            .Select(c => new VisualSqlArchitect.Nodes.Connection(
                c.FromPin.Owner.Id, c.FromPin.Name,
                c.ToPin!.Owner.Id,  c.ToPin.Name))
            .ToList();

        // Determine SELECT bindings —————————————————————————————————————————
        // If a ResultOutput node is present, use its ordered column list.
        // Otherwise fall back to the generic heuristic (all non-boolean outputs).
        var resultOutputNode = _canvas.Nodes.FirstOrDefault(n => n.Type == NodeType.ResultOutput);

        List<VisualSqlArchitect.Nodes.SelectBinding> selectBindings;

        if (resultOutputNode is not null && resultOutputNode.OutputColumnOrder.Count > 0)
        {
            // Ordered columns from the ResultOutput node
            selectBindings = resultOutputNode.GetOrderedColumns()
                .Select(col =>
                {
                    var ownerAlias = _canvas.Nodes.FirstOrDefault(n => n.Id == col.NodeId)?.Alias;
                    return new VisualSqlArchitect.Nodes.SelectBinding(col.NodeId, col.PinName, ownerAlias);
                })
                .ToList();
        }
        else
        {
            // Generic heuristic: all non-boolean output connections
            selectBindings = _canvas.Connections
                .Where(c => c.ToPin?.Owner.Type == NodeType.TableSource == false
                         && c.FromPin.Direction == Nodes.PinDirection.Output
                         && (c.ToPin?.DataType == PinDataType.Any
                          || c.FromPin.DataType != PinDataType.Boolean))
                .Select(c => new VisualSqlArchitect.Nodes.SelectBinding(c.FromPin.Owner.Id, c.FromPin.Name, c.FromPin.Owner.Alias))
                .DistinctBy(b => b.NodeId + b.PinName)
                .ToList();
        }

        var whereBindings = _canvas.Connections
            .Where(c => c.FromPin.DataType == PinDataType.Boolean
                     && c.FromPin.Direction == Nodes.PinDirection.Output
                     && c.ToPin is null)
            .Select(c => new VisualSqlArchitect.Nodes.WhereBinding(c.FromPin.Owner.Id, c.FromPin.Name))
            .ToList();

        return new NodeGraph
        {
            Nodes           = nodes,
            Connections     = connections,
            SelectOutputs   = selectBindings,
            WhereConditions = whereBindings,
            Limit           = 100
        };
    }

    private List<JoinDefinition> BuildJoins(List<NodeViewModel> tableNodes)
    {
        if (tableNodes.Count <= 1) return [];

        var joins = new List<JoinDefinition>();

        // Walk connections between table output pins and non-table input pins
        // that reference another table's column as a FK link
        foreach (var conn in _canvas.Connections)
        {
            if (conn.FromPin.Owner.Type != NodeType.TableSource) continue;
            if (conn.ToPin?.Owner.Type  != NodeType.TableSource) continue;

            // Table → Table connection = explicit JOIN
            var left  = $"{conn.FromPin.Owner.Subtitle}.{conn.FromPin.Name}";
            var right = $"{conn.ToPin.Owner.Subtitle}.{conn.ToPin.Name}";
            joins.Add(new JoinDefinition(conn.ToPin.Owner.Subtitle ?? conn.ToPin.Owner.Title, left, right, "LEFT"));
        }

        return joins;
    }

    private static string FallbackSql(List<NodeViewModel> tables, List<JoinDefinition> joins)
    {
        var from = tables[0].Subtitle ?? tables[0].Title;
        var sb   = new System.Text.StringBuilder($"SELECT *\nFROM {from}");

        foreach (var j in joins)
            sb.Append($"\n{j.Type} JOIN {j.TargetTable} ON {j.LeftColumn} = {j.RightColumn}");

        return sb.ToString();
    }

    // ── Safe Preview — mutating command detection ─────────────────────────────

    private static bool IsMutating(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var trimmed = sql.TrimStart();
        // Skip leading comments (-- ...\n)
        while (trimmed.StartsWith("--"))
        {
            var nl = trimmed.IndexOf('\n');
            trimmed = nl < 0 ? string.Empty : trimmed[(nl + 1)..].TrimStart();
        }
        foreach (var kw in MutatingKeywords)
        {
            if (trimmed.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
            {
                // Ensure it's a full word (not e.g. "CREATES_TABLE")
                if (trimmed.Length == kw.Length || !char.IsLetterOrDigit(trimmed[kw.Length]))
                    return true;
            }
        }
        return false;
    }

    // ── Format SQL ────────────────────────────────────────────────────────────

    public void FormatSql()
    {
        if (string.IsNullOrWhiteSpace(RawSql)) return;

        // Major clause keywords that get their own line
        var clauses = new[] { "SELECT", "FROM", "LEFT JOIN", "RIGHT JOIN", "INNER JOIN",
                              "JOIN", "WHERE", "GROUP BY", "HAVING", "ORDER BY", "LIMIT",
                              "OFFSET", "UNION ALL", "UNION" };

        var sql = RawSql.Trim();

        // Replace each clause keyword with newline + keyword
        foreach (var kw in clauses)
        {
            // Use a regex-free approach: find the keyword surrounded by whitespace
            var idx = 0;
            while (true)
            {
                idx = sql.IndexOf(kw, idx, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;
                // Ensure it's a word boundary (preceded by space/newline or start)
                bool startOk = idx == 0 || char.IsWhiteSpace(sql[idx - 1]);
                bool endOk   = idx + kw.Length >= sql.Length || !char.IsLetterOrDigit(sql[idx + kw.Length]);
                if (startOk && endOk)
                {
                    var before = sql[..idx].TrimEnd();
                    var after  = sql[(idx + kw.Length)..];
                    sql = before + (before.Length > 0 ? "\n" : "") + kw.ToUpperInvariant() + after;
                    idx = before.Length + kw.Length;
                }
                else
                {
                    idx += kw.Length;
                }
            }
        }

        // Indent items in SELECT clause (comma-separated columns → one per line)
        var selectEnd = sql.IndexOf("\nFROM", StringComparison.OrdinalIgnoreCase);
        if (sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && selectEnd > 0)
        {
            var selectBody = sql[6..selectEnd].Trim();
            var columns    = selectBody.Split(',');
            if (columns.Length > 1)
            {
                var formatted = string.Join(",\n    ", columns.Select(c => c.Trim()));
                sql = "SELECT\n    " + formatted + sql[selectEnd..];
            }
        }

        RawSql = sql;
        TokenizeSql(RawSql);
        RaisePropertyChanged(nameof(HasSql));
    }

    // ── Syntax tokenizer ──────────────────────────────────────────────────────

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT","FROM","WHERE","JOIN","LEFT","RIGHT","INNER","OUTER","CROSS","ON",
        "AND","OR","NOT","AS","DISTINCT","GROUP","BY","ORDER","HAVING","LIMIT",
        "OFFSET","BETWEEN","LIKE","IN","IS","NULL","CASE","WHEN","THEN","ELSE",
        "END","EXISTS","UNION","ALL","WITH","FETCH","ROWS","ONLY","ASC","DESC",
        "TOP","CAST","CONVERT","EXTRACT","OVER","PARTITION","ISNULL","COALESCE"
    };

    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPPER","LOWER","TRIM","LENGTH","LEN","CHAR_LENGTH","SUBSTRING","CONCAT",
        "ROUND","ABS","CEIL","CEILING","FLOOR","SUM","AVG","COUNT","MIN","MAX",
        "YEAR","MONTH","DAY","DATE_TRUNC","DATE_DIFF","DATEDIFF","DATEADD",
        "JSON_VALUE","JSON_QUERY","JSON_EXTRACT","PATINDEX","STRING_AGG",
        "GROUP_CONCAT","JSONB_ARRAY_LENGTH","EXTRACT","FORMAT","CONVERT",
        "ISNULL","IFNULL","NULLIF","GREATEST","LEAST","COALESCE","CAST"
    };

    private void TokenizeSql(string sql)
    {
        Tokens.Clear();
        if (string.IsNullOrWhiteSpace(sql)) return;

        // Simple regex-free tokeniser — good enough for highlighting
        int i = 0;
        while (i < sql.Length)
        {
            var ch = sql[i];

            // Comment
            if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                var end = sql.IndexOf('\n', i);
                var text = end < 0 ? sql[i..] : sql[i..end];
                Tokens.Add(new SqlToken(text, SqlTokenKind.Comment));
                i = end < 0 ? sql.Length : end;
                continue;
            }

            // String literal
            if (ch == '\'')
            {
                int j = i + 1;
                while (j < sql.Length && !(sql[j] == '\'' && (j + 1 >= sql.Length || sql[j + 1] != '\'')))
                    j++;
                j = Math.Min(j + 1, sql.Length);
                Tokens.Add(new SqlToken(sql[i..j], SqlTokenKind.Literal));
                i = j;
                continue;
            }

            // Number
            if (char.IsDigit(ch) || (ch == '-' && i + 1 < sql.Length && char.IsDigit(sql[i + 1])))
            {
                int j = i + (ch == '-' ? 1 : 0);
                while (j < sql.Length && (char.IsDigit(sql[j]) || sql[j] == '.')) j++;
                Tokens.Add(new SqlToken(sql[i..j], SqlTokenKind.Literal));
                i = j;
                continue;
            }

            // Identifier / keyword / function
            if (char.IsLetter(ch) || ch == '_' || ch == '"' || ch == '[' || ch == '`')
            {
                char close = ch switch { '"' => '"', '[' => ']', '`' => '`', _ => '\0' };
                int j = i;
                if (close != '\0') { j++; while (j < sql.Length && sql[j] != close) j++; j = Math.Min(j + 1, sql.Length); }
                else { while (j < sql.Length && (char.IsLetterOrDigit(sql[j]) || sql[j] == '_' || sql[j] == '.')) j++; }

                var word = sql[i..j];
                var bare = word.Trim('"', '[', ']', '`');

                // Check if next non-space is '(' → function call
                int k = j; while (k < sql.Length && sql[k] == ' ') k++;
                bool isCall = k < sql.Length && sql[k] == '(';

                SqlTokenKind kind = (isCall && Functions.Contains(bare))  ? SqlTokenKind.Function
                    : Keywords.Contains(bare.Split('.')[0])                ? SqlTokenKind.Keyword
                    : SqlTokenKind.Identifier;

                Tokens.Add(new SqlToken(word, kind));
                i = j;
                continue;
            }

            // Operators
            if (ch is '=' or '<' or '>' or '!' or '~')
            {
                int j = i + 1;
                if (j < sql.Length && sql[j] is '=' or '>' or '<') j++;
                Tokens.Add(new SqlToken(sql[i..j], SqlTokenKind.Operator));
                i = j;
                continue;
            }

            // Punctuation
            if (ch is '(' or ')' or ',' or ';' or '*')
            {
                Tokens.Add(new SqlToken(ch.ToString(), SqlTokenKind.Punctuation));
                i++; continue;
            }

            // Plain (whitespace, newline, other)
            int ws = i;
            while (ws < sql.Length && !char.IsLetterOrDigit(sql[ws]) && sql[ws] is not
                '\'' and not '"' and not '[' and not '`' and not '-'
                and not '=' and not '<' and not '>' and not '~'
                and not '(' and not ')' and not ',' and not ';' and not '*')
                ws++;
            if (ws > i) { Tokens.Add(new SqlToken(sql[i..ws], SqlTokenKind.Plain)); i = ws; }
            else { Tokens.Add(new SqlToken(ch.ToString(), SqlTokenKind.Plain)); i++; }
        }
    }
}

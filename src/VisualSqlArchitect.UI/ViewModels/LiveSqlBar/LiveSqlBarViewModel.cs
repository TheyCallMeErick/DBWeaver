using System.Collections.ObjectModel;
using System.ComponentModel;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

namespace VisualSqlArchitect.UI.ViewModels;

// ─── Token kinds for syntax-highlighted SQL display ───────────────────────────

public enum SqlTokenKind
{
    Keyword, // SELECT, FROM, WHERE, JOIN, AS, ON, AND, OR …
    Identifier, // table.column, alias names
    Literal, // '…', numbers
    Operator, // =, <>, >, BETWEEN, LIKE …
    Punctuation, // ( ) , ;
    Function, // UPPER(, CAST(, JSON_VALUE(…
    Comment, // -- …
    Plain, // whitespace, unknown
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
    private readonly SqlSyntaxHighlighter _highlighter;
    private QueryGraphBuilder? _graphBuilder;
    private readonly object _debounceLock = new();  // Synchronization for _debounce

    private string _rawSql = string.Empty;
    private string _displaySql = string.Empty;
    private bool _isValid = true;
    private bool _isCompiling;
    private string? _compileError;
    private bool _isMutatingCommand;
    private DatabaseProvider _provider = DatabaseProvider.Postgres;

    // ── Observable collections ────────────────────────────────────────────────

    public ObservableCollection<SqlToken> Tokens { get; } = [];
    public ObservableCollection<string> ErrorHints { get; } = [];
    public ObservableCollection<GuardIssue> GuardIssues { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string RawSql
    {
        get => _rawSql;
        private set
        {
            Set(ref _rawSql, value);
            _canvas.UpdateQueryText(value);
        }
    }

    public string DisplaySql
    {
        get => _displaySql;
        private set => Set(ref _displaySql, value);
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
        set
        {
            Set(ref _provider, value);
            Recompile();
        }
    }

    public string ProviderLabel =>
        _provider switch
        {
            DatabaseProvider.SqlServer => "SQL Server",
            DatabaseProvider.MySql => "MySQL",
            DatabaseProvider.SQLite => "SQLite",
            _ => "PostgreSQL",
        };

    public bool HasSql => !string.IsNullOrWhiteSpace(RawSql);

    public bool IsMutatingCommand
    {
        get => _isMutatingCommand;
        private set => Set(ref _isMutatingCommand, value);
    }

    public string? BlockedReason =>
        IsMutatingCommand
            ? "Blocked in Safe Preview Mode — SQL contains a data-mutating command (INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE)"
            : null;

    public bool HasGuardWarning => GuardIssues.Count > 0;

    // ── Constructor ───────────────────────────────────────────────────────────

    // Tracks per-node PropertyChanged handlers so they can be unsubscribed on node removal.
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodeHandlers = new();

    public LiveSqlBarViewModel(CanvasViewModel canvas)
    {
        _canvas = canvas;
        _highlighter = new SqlSyntaxHighlighter();

        canvas.Connections.CollectionChanged += (_, _) => ScheduleRecompile();

        canvas.Nodes.CollectionChanged += (_, e) =>
        {
            ScheduleRecompile();

            // Subscribe new nodes — skip pure visual/UI properties that don't affect SQL
            if (e.NewItems is not null)
                foreach (NodeViewModel vm in e.NewItems)
                    SubscribeNode(vm);

            // Unsubscribe removed nodes to prevent memory leaks
            if (e.OldItems is not null)
                foreach (NodeViewModel vm in e.OldItems)
                    UnsubscribeNode(vm);
        };

        Recompile();
    }

    private void SubscribeNode(NodeViewModel vm)
    {
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName is nameof(NodeViewModel.Position)
                or nameof(NodeViewModel.IsSelected)
                or nameof(NodeViewModel.IsOrphan)
                or nameof(NodeViewModel.IsHovered)
                or nameof(NodeViewModel.Width))
                return;
            ScheduleRecompile();
        };
        vm.PropertyChanged += handler;
        _nodeHandlers[vm] = handler;
    }

    private void UnsubscribeNode(NodeViewModel vm)
    {
        if (_nodeHandlers.TryGetValue(vm, out PropertyChangedEventHandler? handler))
        {
            vm.PropertyChanged -= handler;
            _nodeHandlers.Remove(vm);
        }
    }

    // ── Debounced recompile ───────────────────────────────────────────────────

    private CancellationTokenSource? _debounce;

    private void ScheduleRecompile()
    {
        lock (_debounceLock)
        {
            _debounce?.Cancel();
            _debounce?.Dispose();
            _debounce = new CancellationTokenSource();
            CancellationToken token = _debounce.Token;

            // 120ms debounce — avoids recompiling on every intermediate drag step
            Task.Delay(120, token)
                .ContinueWith(
                    _ =>
                    {
                        if (!token.IsCancellationRequested)
                            Avalonia.Threading.Dispatcher.UIThread.Post(Recompile);
                    },
                    TaskScheduler.Default
                );
        }
    }

    // ── Compilation ───────────────────────────────────────────────────────────

    public void Recompile()
    {
        ErrorHints.Clear();
        GuardIssues.Clear();
        IsCompiling = true;

        try
        {
            // Apply in-flight edits from the property panel before reading node parameters.
            _canvas.PropertyPanel.CommitDirty();

            _graphBuilder = new QueryGraphBuilder(_canvas, _provider);

            // ── Portability pre-check ─────────────────────────────────────────
            List<GuardIssue> portabilityIssues = QueryValidationService.CheckPortability(
                _canvas.Nodes.Select(n => n.Type),
                _provider
            );
            foreach (GuardIssue issue in portabilityIssues)
                GuardIssues.Add(issue);

            (string sql, List<string> errors) = _graphBuilder.BuildSql();

            RawSql = sql;
            DisplaySql = FormatSqlText(sql);
            IsValid = errors.Count == 0;
            CompileError = errors.Count > 0 ? errors[0] : null;

            // Detect mutating commands — block preview execution
            IsMutatingCommand = QueryValidationService.IsMutating(sql);
            RaisePropertyChanged(nameof(BlockedReason));

            // Run guardrails (only when not a mutating command)
            if (!IsMutatingCommand)
            {
                foreach (GuardIssue issue in QueryGuardrails.Check(sql))
                    GuardIssues.Add(issue);
            }
            RaisePropertyChanged(nameof(HasGuardWarning));

            foreach (string err in errors)
                ErrorHints.Add(err);

            SqlSyntaxHighlighter.Tokenize(DisplaySql, Tokens);
        }
        catch (Exception ex)
        {
            RawSql = string.Empty;
            DisplaySql = string.Empty;
            IsValid = false;
            CompileError = ex.Message;
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

    // ── Format SQL ────────────────────────────────────────────────────────────

    public void FormatSql()
    {
        if (string.IsNullOrWhiteSpace(RawSql))
            return;

        string sql = FormatSqlText(RawSql);
        RawSql = sql;
        DisplaySql = sql;
        SqlSyntaxHighlighter.Tokenize(sql, Tokens);
        RaisePropertyChanged(nameof(HasSql));
    }

    private static string FormatSqlText(string sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
            return string.Empty;

        // Major clause keywords that get their own line
        string[] clauses = new[]
        {
            "SELECT",
            "FROM",
            "LEFT JOIN",
            "RIGHT JOIN",
            "INNER JOIN",
            "JOIN",
            "WHERE",
            "GROUP BY",
            "HAVING",
            "ORDER BY",
            "LIMIT",
            "OFFSET",
            "UNION ALL",
            "UNION",
        };

        string sql = sqlText.Trim();

        // Replace each clause keyword with newline + keyword
        foreach (string? kw in clauses)
        {
            int idx = 0;
            while (true)
            {
                idx = sql.IndexOf(kw, idx, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;
                bool startOk = idx == 0 || char.IsWhiteSpace(sql[idx - 1]);
                bool endOk =
                    idx + kw.Length >= sql.Length || !char.IsLetterOrDigit(sql[idx + kw.Length]);
                if (startOk && endOk)
                {
                    string before = sql[..idx].TrimEnd();
                    string after = sql[(idx + kw.Length)..];
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
        int selectEnd = sql.IndexOf("\nFROM", StringComparison.OrdinalIgnoreCase);
        if (sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && selectEnd > 0)
        {
            string selectBody = sql[6..selectEnd].Trim();
            string[] columns = selectBody.Split(',');
            if (columns.Length > 1)
            {
                string formatted = string.Join(",\n    ", columns.Select(c => c.Trim()));
                sql = "SELECT\n    " + formatted + sql[selectEnd..];
            }
        }

        return sql;
    }
}

using DBWeaver.Core;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.ViewModels;

public sealed class DdlExecuteDialogViewModel(string sqlPreview) : ViewModelBase
{
    private static readonly Regex DropTableRegex = new(@"\bDROP\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private bool _stopOnError = true;
    private bool _isExecuting;
    private bool _hasResult;
    private bool _isSuccess;
    private bool _confirmDestructiveExecution;
    private string _resultSummary = string.Empty;
    private string _resultDetails = string.Empty;

    public string SqlPreview { get; } = sqlPreview;
    public ObservableCollection<DdlStatementPreviewItem> StatementPreviews { get; } = BuildStatementPreviews(sqlPreview);
    public bool HasDestructiveStatements => StatementPreviews.Any(s => s.IsDestructive);

    public bool ConfirmDestructiveExecution
    {
        get => _confirmDestructiveExecution;
        set
        {
            if (!Set(ref _confirmDestructiveExecution, value))
                return;

            RaisePropertyChanged(nameof(CanExecute));
        }
    }

    public bool CanExecute => !IsExecuting && (!HasDestructiveStatements || ConfirmDestructiveExecution);

    public bool StopOnError
    {
        get => _stopOnError;
        set => Set(ref _stopOnError, value);
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        set
        {
            if (!Set(ref _isExecuting, value))
                return;

            RaisePropertyChanged(nameof(CanExecute));
        }
    }

    public bool HasResult
    {
        get => _hasResult;
        private set => Set(ref _hasResult, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        private set => Set(ref _isSuccess, value);
    }

    public string ResultSummary
    {
        get => _resultSummary;
        private set => Set(ref _resultSummary, value);
    }

    public string ResultDetails
    {
        get => _resultDetails;
        private set => Set(ref _resultDetails, value);
    }

    public void ApplyResult(DdlExecutionResult result)
    {
        int ok = result.Statements.Count(s => s.Success);
        int fail = result.Statements.Count - ok;

        HasResult = true;
        IsSuccess = result.Success;
        ResultSummary = string.Format(
            L(
                "ddl.execute.result.summary",
                "Statements: {0} | Success: {1} | Failures: {2} | Time: {3:0}ms"
            ),
            result.Statements.Count,
            ok,
            fail,
            result.ExecutionTime?.TotalMilliseconds ?? 0
        );
        ResultDetails = string.Join(
            "\n",
            result.Statements.Select(s =>
                s.Success
                    ? string.Format(
                        L("ddl.execute.result.okLine", "[{0}] OK   | rows={1} | {2}"),
                        s.StatementIndex,
                        s.RowsAffected ?? 0,
                        SingleLine(s.Sql)
                    )
                    : string.Format(
                        L("ddl.execute.result.failLine", "[{0}] FAIL | {1} | {2}"),
                        s.StatementIndex,
                        s.ErrorMessage,
                        SingleLine(s.Sql)
                    )
            )
        );
    }

    public void ApplyError(Exception ex)
    {
        HasResult = true;
        IsSuccess = false;
        ResultSummary = L("ddl.execute.result.failed", "Failed to execute DDL.");
        ResultDetails = ex.Message;
    }

    public void ApplyCancelled()
    {
        HasResult = true;
        IsSuccess = false;
        ResultSummary = L("ddl.execute.result.cancelled", "Execution cancelled by the user.");
        ResultDetails = L(
            "ddl.execute.result.cancelledDetails",
            "DDL execution was interrupted before completion."
        );
    }

    private static string SingleLine(string sql)
    {
        string compact = sql.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 140 ? compact : compact[..140] + "...";
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static ObservableCollection<DdlStatementPreviewItem> BuildStatementPreviews(string sql)
    {
        IReadOnlyList<string> statements = SplitSqlStatements(sql);
        var items = new ObservableCollection<DdlStatementPreviewItem>();

        for (int i = 0; i < statements.Count; i++)
        {
            string statement = statements[i];
            bool isDestructive = DropTableRegex.IsMatch(statement);
            items.Add(new DdlStatementPreviewItem(i + 1, statement, isDestructive));
        }

        return items;
    }

    private static IReadOnlyList<string> SplitSqlStatements(string sql)
    {
        var statements = new List<string>();
        var sb = new StringBuilder();

        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inLineComment = false;
        bool inBlockComment = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                sb.Append(c);
                if (c == '\n')
                    inLineComment = false;
                continue;
            }

            if (inBlockComment)
            {
                sb.Append(c);
                if (c == '*' && next == '/')
                {
                    sb.Append(next);
                    i++;
                    inBlockComment = false;
                }
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote)
            {
                if (c == '-' && next == '-')
                {
                    sb.Append(c);
                    sb.Append(next);
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    sb.Append(c);
                    sb.Append(next);
                    i++;
                    inBlockComment = true;
                    continue;
                }
            }

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                sb.Append(c);
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                sb.Append(c);
                continue;
            }

            if (c == ';' && !inSingleQuote && !inDoubleQuote)
            {
                string statement = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(statement))
                    statements.Add(statement);
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        string tail = sb.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
            statements.Add(tail);

        return statements;
    }
}

public sealed record DdlStatementPreviewItem(int Index, string Sql, bool IsDestructive)
{
    public string CompactSql
    {
        get
        {
            string compact = Sql.Replace("\r", " ").Replace("\n", " ").Trim();
            if (compact.Length > 150)
                compact = compact[..150] + "...";

            return $"[{Index}] {compact}";
        }
    }
}

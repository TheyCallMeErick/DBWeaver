using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

// ── Explain step ───────────────────────────────────────────────────────────────

public sealed class ExplainStep
{
    public int StepNumber { get; init; }
    public string Operation { get; init; } = "";
    public string? Detail { get; init; }
    public double? EstimatedCost { get; init; }
    public long? EstimatedRows { get; init; }
    public int IndentLevel { get; init; }

    // Alert
    public bool IsExpensive { get; init; }
    public string AlertLabel { get; init; } = "";

    // Computed for XAML
    public double IndentMargin => IndentLevel * 18.0;
    public string CostText => EstimatedCost.HasValue ? $"{EstimatedCost:F2}" : "–";
    public string RowsText => EstimatedRows.HasValue ? $"{EstimatedRows:N0}" : "–";
    public string AlertColor => AlertLabel switch
    {
        "SEQ SCAN" => "#FBBF24",
        "SORT"     => "#FB923C",
        "HASH"     => "#60A5FA",
        "LOOP"     => "#A78BFA",
        _          => "#6B7280",
    };
    public bool HasAlert => !string.IsNullOrEmpty(AlertLabel);
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

/// <summary>
/// Provides an in-panel EXPLAIN plan inspector.
/// Generates per-provider EXPLAIN SQL, parses the output into structured steps,
/// and highlights expensive operations (Seq Scan, Sort, Hash Join).
/// </summary>
public sealed class ExplainPlanViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;

    private bool _isVisible;
    private bool _isLoading;
    private string _sql = "";
    private DatabaseProvider _provider = DatabaseProvider.Postgres;
    private string? _errorMessage;

    public ExplainPlanViewModel(CanvasViewModel canvas) => _canvas = canvas;

    // ── Visibility ────────────────────────────────────────────────────────────

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            Set(ref _isLoading, value);
            RaisePropertyChanged(nameof(HasData));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            Set(ref _errorMessage, value);
            RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    // ── SQL / Provider ────────────────────────────────────────────────────────

    public string Sql
    {
        get => _sql;
        private set => Set(ref _sql, value);
    }

    public DatabaseProvider Provider
    {
        get => _provider;
        private set
        {
            Set(ref _provider, value);
            RaisePropertyChanged(nameof(ProviderLabel));
            RaisePropertyChanged(nameof(ExplainHeader));
        }
    }

    public string ProviderLabel => _provider switch
    {
        DatabaseProvider.Postgres  => "PostgreSQL",
        DatabaseProvider.MySql     => "MySQL",
        DatabaseProvider.SqlServer => "SQL Server",
        DatabaseProvider.SQLite    => "SQLite",
        _                          => _provider.ToString(),
    };

    public string ExplainHeader => _provider switch
    {
        DatabaseProvider.Postgres  => "EXPLAIN (FORMAT TEXT)",
        DatabaseProvider.MySql     => "EXPLAIN",
        DatabaseProvider.SqlServer => "SET SHOWPLAN_TEXT ON",
        DatabaseProvider.SQLite    => "EXPLAIN QUERY PLAN",
        _                          => "EXPLAIN",
    };

    // ── Results ───────────────────────────────────────────────────────────────

    public ObservableCollection<ExplainStep> Steps { get; } = [];

    public bool HasData => Steps.Count > 0 && !_isLoading;

    public int AlertCount => Steps.Count(s => s.IsExpensive);
    public bool HasAlerts => AlertCount > 0;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        Sql      = _canvas.LiveSql.RawSql;
        Provider = _canvas.LiveSql.Provider;
        IsVisible = true;

        // Auto-run on open with explicit exception handling
        _ = RunExplainAsyncSafe();
    }

    private async Task RunExplainAsyncSafe()
    {
        try
        {
            await RunExplainAsync();
        }
        catch (Exception ex)
        {
            // Log or handle exception in fire-and-forget context
            // Prevents unhandled exceptions from crashing the app
            ErrorMessage = $"Explain plan error: {ex.Message}";
            IsLoading = false;
            System.Diagnostics.Debug.WriteLine($"[ExplainPlan] Unhandled exception in fire-and-forget: {ex}");
        }
    }

    public void Close() => IsVisible = false;

    // ── Run ───────────────────────────────────────────────────────────────────

    public async Task RunExplainAsync()
    {
        if (string.IsNullOrWhiteSpace(_sql))
        {
            ErrorMessage = "No SQL to explain. Build a query on the canvas first.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        Steps.Clear();
        RaisePropertyChanged(nameof(HasData));
        RaisePropertyChanged(nameof(AlertCount));
        RaisePropertyChanged(nameof(HasAlerts));

        // Refresh in case SQL changed since Open()
        Sql      = _canvas.LiveSql.RawSql;
        Provider = _canvas.LiveSql.Provider;

        try
        {
            // Simulate network/DB round-trip
            await Task.Delay(AppConstants.ExplainPlanRefreshMs);

            var steps = _provider switch
            {
                DatabaseProvider.Postgres  => SimulatePostgres(_sql),
                DatabaseProvider.MySql     => SimulateMySql(_sql),
                DatabaseProvider.SqlServer => SimulateSqlServer(_sql),
                _                          => SimulatePostgres(_sql),
            };

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var s in steps)
                    Steps.Add(s);

                RaisePropertyChanged(nameof(HasData));
                RaisePropertyChanged(nameof(AlertCount));
                RaisePropertyChanged(nameof(HasAlerts));
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Simulation helpers ────────────────────────────────────────────────────

    private record SqlContext(
        List<string> Tables,
        bool HasWhere,
        bool HasOrderBy,
        bool HasLimit,
        bool HasJoin,
        int JoinCount
    );

    private static SqlContext ParseSql(string sql)
    {
        string up = sql.ToUpperInvariant();

        // Extract table names after FROM / JOIN
        var tableMatches = Regex.Matches(sql,
            @"(?:FROM|JOIN)\s+([a-zA-Z_][a-zA-Z0-9_]*)",
            RegexOptions.IgnoreCase);
        var tables = tableMatches
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (tables.Count == 0)
            tables.Add("table1");

        int joinCount = Regex.Matches(up, @"\bJOIN\b").Count;

        return new SqlContext(
            Tables: tables,
            HasWhere:   up.Contains("WHERE"),
            HasOrderBy: up.Contains("ORDER BY"),
            HasLimit:   up.Contains("LIMIT") || up.Contains("TOP"),
            HasJoin:    joinCount > 0,
            JoinCount:  joinCount
        );
    }

    // ── PostgreSQL simulation ─────────────────────────────────────────────────

    private static List<ExplainStep> SimulatePostgres(string sql)
    {
        var ctx   = ParseSql(sql);
        var steps = new List<ExplainStep>();
        var rng   = new Random(sql.Length ^ 0x1A2B3C);
        int stepNo = 1;

        double baseCost  = rng.Next(200, 600);
        double scanCost1 = rng.Next(100, 400);
        long   scanRows1 = rng.Next(1000, 50000);
        double totalCost = baseCost + scanCost1;

        // Top-level node
        if (ctx.HasLimit)
        {
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = "Limit",
                Detail        = $"cost=0.00..{totalCost:F2}  rows=100  width=64",
                EstimatedCost = totalCost,
                EstimatedRows = 100,
                IndentLevel   = 0,
            });
        }

        int topIndent = ctx.HasLimit ? 1 : 0;

        if (ctx.HasJoin)
        {
            string joinType = ctx.JoinCount > 1 ? "Hash Join" : "Hash Join";
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = joinType,
                Detail        = $"cost={baseCost:F2}..{totalCost:F2}  rows={scanRows1 / 10}  width=64",
                EstimatedCost = totalCost,
                EstimatedRows = scanRows1 / 10,
                IndentLevel   = topIndent,
                IsExpensive   = false,
                AlertLabel    = "HASH",
            });

            if (ctx.Tables.Count >= 2)
            {
                steps.Add(new ExplainStep
                {
                    StepNumber    = stepNo++,
                    Operation     = $"Hash Cond: ({ctx.Tables[0]}.id = {ctx.Tables[1]}.id)",
                    EstimatedCost = null,
                    EstimatedRows = null,
                    IndentLevel   = topIndent + 1,
                });
            }

            // First table scan
            bool useIndex1 = rng.Next(2) == 0;
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = useIndex1 ? $"Index Scan on {ctx.Tables[0]}" : $"Seq Scan on {ctx.Tables[0]}",
                Detail        = $"cost=0.00..{scanCost1:F2}  rows={scanRows1}  width=32",
                EstimatedCost = scanCost1,
                EstimatedRows = scanRows1,
                IndentLevel   = topIndent + 1,
                IsExpensive   = !useIndex1,
                AlertLabel    = useIndex1 ? "" : "SEQ SCAN",
            });

            if (ctx.HasWhere)
            {
                steps.Add(new ExplainStep
                {
                    StepNumber  = stepNo++,
                    Operation   = "Filter: (condition)",
                    IndentLevel = topIndent + 2,
                });
            }

            // Second table hash + scan
            double scanCost2 = rng.Next(50, 200);
            long   scanRows2 = rng.Next(500, 10000);
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = "Hash",
                Detail        = $"cost={scanCost2:F2}..{scanCost2:F2}  rows={scanRows2}  width=32",
                EstimatedCost = scanCost2,
                EstimatedRows = scanRows2,
                IndentLevel   = topIndent + 1,
            });

            string tbl2 = ctx.Tables.Count >= 2 ? ctx.Tables[1] : ctx.Tables[0];
            bool useIndex2 = rng.Next(2) == 0;
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = useIndex2 ? $"Index Scan on {tbl2}" : $"Seq Scan on {tbl2}",
                Detail        = $"cost=0.00..{scanCost2:F2}  rows={scanRows2}  width=32",
                EstimatedCost = scanCost2,
                EstimatedRows = scanRows2,
                IndentLevel   = topIndent + 2,
                IsExpensive   = !useIndex2,
                AlertLabel    = useIndex2 ? "" : "SEQ SCAN",
            });
        }
        else
        {
            // Single table scan
            bool useIndex = rng.Next(3) != 0;
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = useIndex ? $"Index Scan on {ctx.Tables[0]}" : $"Seq Scan on {ctx.Tables[0]}",
                Detail        = $"cost=0.00..{scanCost1:F2}  rows={scanRows1}  width=64",
                EstimatedCost = scanCost1,
                EstimatedRows = scanRows1,
                IndentLevel   = topIndent,
                IsExpensive   = !useIndex,
                AlertLabel    = useIndex ? "" : "SEQ SCAN",
            });

            if (ctx.HasWhere)
            {
                steps.Add(new ExplainStep
                {
                    StepNumber  = stepNo++,
                    Operation   = "Filter: (condition)",
                    IndentLevel = topIndent + 1,
                });
            }
        }

        if (ctx.HasOrderBy)
        {
            steps.Insert(ctx.HasLimit ? 1 : 0, new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = "Sort",
                Detail        = $"cost={baseCost / 2:F2}..{baseCost:F2}  rows={scanRows1 / 5}  width=64",
                EstimatedCost = baseCost,
                EstimatedRows = scanRows1 / 5,
                IndentLevel   = topIndent,
                IsExpensive   = true,
                AlertLabel    = "SORT",
            });
        }

        return steps;
    }

    // ── MySQL simulation ──────────────────────────────────────────────────────

    private static List<ExplainStep> SimulateMySql(string sql)
    {
        var ctx   = ParseSql(sql);
        var steps = new List<ExplainStep>();
        var rng   = new Random(sql.Length ^ 0x2C3D4E);
        int id    = 1;

        foreach (string table in ctx.Tables)
        {
            bool useIndex = rng.Next(3) != 0;
            long rows     = rng.Next(100, 50000);
            bool hasExtra = ctx.HasWhere && table == ctx.Tables[0];

            string type   = useIndex ? "ref"  : "ALL";
            string key    = useIndex ? "idx_" + table : "NULL";
            string extra  = hasExtra ? "Using where" : (ctx.HasOrderBy ? "Using filesort" : "");

            bool isFilesort = extra.Contains("filesort");

            steps.Add(new ExplainStep
            {
                StepNumber    = id,
                Operation     = table,
                Detail        = $"select_type=SIMPLE  type={type}  key={key}  Extra={extra}",
                EstimatedRows = rows,
                IndentLevel   = 0,
                IsExpensive   = !useIndex || isFilesort,
                AlertLabel    = !useIndex ? "SEQ SCAN" : (isFilesort ? "SORT" : ""),
            });

            id++;
        }

        return steps;
    }

    // ── SQL Server simulation ─────────────────────────────────────────────────

    private static List<ExplainStep> SimulateSqlServer(string sql)
    {
        var ctx   = ParseSql(sql);
        var steps = new List<ExplainStep>();
        var rng   = new Random(sql.Length ^ 0x3E4F5A);
        int stepNo = 1;

        if (ctx.HasLimit)
        {
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = "Top",
                Detail        = "TOP EXPRESSION: (100)",
                EstimatedCost = 0.01,
                EstimatedRows = 100,
                IndentLevel   = 0,
            });
        }

        int topIndent = ctx.HasLimit ? 1 : 0;

        if (ctx.HasJoin)
        {
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = "Nested Loops",
                Detail        = "Inner Join",
                EstimatedCost = rng.Next(100, 500),
                EstimatedRows = rng.Next(500, 5000),
                IndentLevel   = topIndent,
                IsExpensive   = false,
                AlertLabel    = "LOOP",
            });

            foreach (string table in ctx.Tables.Take(2))
            {
                bool useIndex = rng.Next(3) != 0;
                long rows     = rng.Next(500, 30000);
                steps.Add(new ExplainStep
                {
                    StepNumber    = stepNo++,
                    Operation     = useIndex ? $"Index Seek ({table})" : $"Table Scan ({table})",
                    Detail        = ctx.HasWhere && table == ctx.Tables[0] ? "Predicate: condition" : null,
                    EstimatedCost = rng.Next(10, 300),
                    EstimatedRows = rows,
                    IndentLevel   = topIndent + 1,
                    IsExpensive   = !useIndex,
                    AlertLabel    = useIndex ? "" : "SEQ SCAN",
                });
            }
        }
        else
        {
            bool useIndex = rng.Next(3) != 0;
            long rows     = rng.Next(1000, 50000);
            steps.Add(new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = useIndex ? $"Index Seek ({ctx.Tables[0]})" : $"Table Scan ({ctx.Tables[0]})",
                Detail        = ctx.HasWhere ? "Predicate: condition" : null,
                EstimatedCost = rng.Next(50, 400),
                EstimatedRows = rows,
                IndentLevel   = topIndent,
                IsExpensive   = !useIndex,
                AlertLabel    = useIndex ? "" : "SEQ SCAN",
            });
        }

        if (ctx.HasOrderBy)
        {
            steps.Insert(ctx.HasLimit ? 1 : 0, new ExplainStep
            {
                StepNumber    = stepNo++,
                Operation     = "Sort",
                Detail        = "ORDER BY",
                EstimatedCost = rng.Next(100, 800),
                EstimatedRows = rng.Next(500, 5000),
                IndentLevel   = topIndent,
                IsExpensive   = true,
                AlertLabel    = "SORT",
            });
        }

        return steps;
    }
}

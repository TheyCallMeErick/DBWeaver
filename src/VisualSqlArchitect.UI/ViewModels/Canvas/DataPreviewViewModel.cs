using System.Data;
using VisualSqlArchitect.UI.Services;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

// ── Tab selection ──────────────────────────────────────────────────────────────

public enum PreviewTab { DataPreview, LiveSql }

// ── Execution state machine ────────────────────────────────────────────────────

public enum PreviewExecutionState
{
    Idle,
    Running,
    Done,
    Cancelled,
    Failed,
}

/// <summary>
/// Manages the SQL execution preview panel.
/// Displays query results, execution diagnostics, error messages, loading states,
/// cancellation feedback, and elapsed-time metrics.
/// </summary>
public sealed class DataPreviewViewModel : ViewModelBase
{
    private bool _isVisible;
    private string? _errorMsg;
    private string _queryText = "";
    private DataTable? _data;
    private double _panelHeight = 280;
    private int _rows;
    private long _ms;
    private DiagnosticResult? _diagnostic;
    private PreviewExecutionState _state = PreviewExecutionState.Idle;
    private long _elapsedMs;
    private PreviewTab _activeTab = PreviewTab.DataPreview;

    // ── Visibility ────────────────────────────────────────────────────────────

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    // ── Tab selection ─────────────────────────────────────────────────────────

    public PreviewTab ActiveTab
    {
        get => _activeTab;
        set
        {
            Set(ref _activeTab, value);
            RaisePropertyChanged(nameof(ShowDataPreview));
            RaisePropertyChanged(nameof(ShowLiveSql));
        }
    }

    public bool ShowDataPreview => _activeTab == PreviewTab.DataPreview;
    public bool ShowLiveSql => _activeTab == PreviewTab.LiveSql;

    // ── Execution state ───────────────────────────────────────────────────────

    public PreviewExecutionState CurrentState
    {
        get => _state;
        private set
        {
            Set(ref _state, value);
            RaisePropertyChanged(nameof(IsLoading));
            RaisePropertyChanged(nameof(IsCancelled));
            RaisePropertyChanged(nameof(StatusText));
            RaisePropertyChanged(nameof(StatusColor));
        }
    }

    /// <summary>Whether a query is currently executing (alias for Running state).</summary>
    public bool IsLoading => _state == PreviewExecutionState.Running;

    /// <summary>Whether the last run was explicitly cancelled.</summary>
    public bool IsCancelled => _state == PreviewExecutionState.Cancelled;

    // ── Live elapsed time (updated during run) ────────────────────────────────

    public long ElapsedMs
    {
        get => _elapsedMs;
        set
        {
            Set(ref _elapsedMs, value);
            if (_state == PreviewExecutionState.Running)
                RaisePropertyChanged(nameof(StatusText));
        }
    }

    // ── Error ─────────────────────────────────────────────────────────────────

    public string? ErrorMessage
    {
        get => _errorMsg;
        set
        {
            Set(ref _errorMsg, value);
            RaisePropertyChanged(nameof(StatusText));
        }
    }

    // ── Query text ────────────────────────────────────────────────────────────

    public string QueryText
    {
        get => _queryText;
        set => Set(ref _queryText, value);
    }

    // ── Results ───────────────────────────────────────────────────────────────

    public DataTable? ResultData
    {
        get => _data;
        set
        {
            System.Console.WriteLine($"[ResultData Property] Setting: old={_data?.Rows.Count ?? 0} rows, new={value?.Rows.Count ?? 0} rows");
            if (Set(ref _data, value))
            {
                System.Console.WriteLine($"[ResultData Property] Changed=true, raising PropertyChanged");
                RaisePropertyChanged(nameof(ResultView));
                RaisePropertyChanged(nameof(HasData));
                RaisePropertyChanged(nameof(StatusText));
                System.Console.WriteLine($"[ResultData Property] After raising: HasData={HasData}");
            }
            else
            {
                System.Console.WriteLine($"[ResultData Property] Changed=false (same reference)");
            }
        }
    }

    public double PanelHeight
    {
        get => _panelHeight;
        set => Set(ref _panelHeight, value);
    }

    public int RowCount
    {
        get => _rows;
        set => Set(ref _rows, value);
    }

    public long ExecutionMs
    {
        get => _ms;
        set => Set(ref _ms, value);
    }

    /// <summary>Returns DataView for Avalonia DataGrid binding (properly enumerable).</summary>
    public System.Data.DataView? ResultView
    {
        get
        {
            if (_data is null)
            {
                System.Console.WriteLine("[ResultView] _data is null, returning null");
                return null;
            }
            var view = _data.DefaultView;
            System.Console.WriteLine($"[ResultView] Returning DataView with {view.Count} rows from DataTable with {_data.Rows.Count} rows");
            return view;
        }
    }

    public bool HasData
    {
        get
        {
            bool result = _data is { Rows.Count: > 0 };
            System.Console.WriteLine($"[HasData Getter] value={result}, _data={_data?.Rows.Count ?? 0} rows");
            return result;
        }
    }

    // ── Status text + color ───────────────────────────────────────────────────

    public string StatusText => _state switch
    {
        PreviewExecutionState.Running   => $"Running… {_elapsedMs}ms",
        PreviewExecutionState.Cancelled => "Cancelled",
        PreviewExecutionState.Failed    => "Error",
        PreviewExecutionState.Done      => $"{_rows} rows · {_ms}ms",
        _                               => "Ready",
    };

    public string StatusColor => _state switch
    {
        PreviewExecutionState.Running   => "#60A5FA",
        PreviewExecutionState.Cancelled => "#FBBF24",
        PreviewExecutionState.Failed    => "#EF4444",
        PreviewExecutionState.Done      => "#4ADE80",
        _                               => "#4A5568",
    };

    // ── Diagnostic ────────────────────────────────────────────────────────────

    public DiagnosticResult? Diagnostic
    {
        get => _diagnostic;
        private set
        {
            Set(ref _diagnostic, value);
            RaisePropertyChanged(nameof(HasDiagnostic));
            RaisePropertyChanged(nameof(DiagnosticIcon));
            RaisePropertyChanged(nameof(DiagnosticLabel));
            RaisePropertyChanged(nameof(DiagnosticSuggestion));
            RaisePropertyChanged(nameof(DiagnosticTechnical));
            RaisePropertyChanged(nameof(HasTechnicalDetail));
        }
    }

    public bool HasDiagnostic => _diagnostic is not null;
    public string DiagnosticIcon => _diagnostic?.CategoryIcon ?? "⚠️";
    public string DiagnosticLabel => _diagnostic?.CategoryLabel ?? "Error";
    public string DiagnosticSuggestion => _diagnostic?.Suggestion ?? string.Empty;
    public string? DiagnosticTechnical => _diagnostic?.TechnicalDetail;
    public bool HasTechnicalDetail => !string.IsNullOrWhiteSpace(_diagnostic?.TechnicalDetail);

    // ── Public API (called by PreviewService) ─────────────────────────────────

    public void Toggle() => IsVisible = !IsVisible;

    /// <summary>Transitions to Running state; resets previous results.</summary>
    public void ShowLoading(string sql)
    {
        System.Diagnostics.Debug.WriteLine($"[DataPreviewViewModel] ShowLoading called, panel will be visible");
        QueryText = sql;
        CurrentState = PreviewExecutionState.Running;
        ErrorMessage = null;
        ResultData = null;
        Diagnostic = null;
        ElapsedMs = 0;
        IsVisible = true;
        System.Diagnostics.Debug.WriteLine($"[DataPreviewViewModel] IsVisible={IsVisible}");
    }

    /// <summary>Updates the live elapsed time counter shown in the status chip.</summary>
    public void UpdateElapsed(long ms) => ElapsedMs = ms;

    /// <summary>Transitions to Done state and displays the result grid.</summary>
    public void ShowResults(DataTable dt, long ms)
    {
        System.Console.WriteLine($"[ShowResults] START - Input: {dt?.Rows.Count ?? 0} rows, {ms}ms");
        System.Diagnostics.Debug.WriteLine($"[DataPreviewViewModel] ShowResults called with {dt?.Rows.Count ?? 0} rows, {ms}ms");

        System.Console.WriteLine($"[ShowResults] Before ResultData assignment: _data={_data?.Rows.Count ?? 0}, HasData={HasData}");
        ResultData = dt;
        System.Console.WriteLine($"[ShowResults] After ResultData assignment: _data={_data?.Rows.Count ?? 0}, HasData={HasData}");
        System.Diagnostics.Debug.WriteLine($"[DataPreviewViewModel] ResultData set to DataTable, HasData={HasData}, Rows={dt?.Rows.Count}");

        RowCount = dt.Rows.Count;
        System.Console.WriteLine($"[ShowResults] RowCount set to {RowCount}");

        ExecutionMs = ms;
        ElapsedMs = ms;
        ErrorMessage = null;
        Diagnostic = null;

        System.Console.WriteLine($"[ShowResults] Before CurrentState=Done: StatusText={StatusText}");
        CurrentState = PreviewExecutionState.Done;
        System.Console.WriteLine($"[ShowResults] After CurrentState=Done: StatusText={StatusText}, HasData={HasData}, IsVisible={IsVisible}");

        System.Diagnostics.Debug.WriteLine($"[DataPreviewViewModel] ✓ ShowResults completed: State=Done, HasData={HasData}, IsVisible={IsVisible}");
        System.Console.WriteLine($"[ShowResults] DONE\n");
    }

    /// <summary>Transitions to Failed state with structured diagnostic.</summary>
    public void ShowError(string msg, Exception? ex = null)
    {
        Diagnostic = ErrorDiagnostics.Classify(msg, ex);
        ErrorMessage = Diagnostic.FriendlyMessage;
        CurrentState = PreviewExecutionState.Failed;
    }

    /// <summary>Transitions to Cancelled state; keeps previous result data visible.</summary>
    public void ShowCancelled()
    {
        ErrorMessage = null;
        Diagnostic = null;
        CurrentState = PreviewExecutionState.Cancelled;
    }
}

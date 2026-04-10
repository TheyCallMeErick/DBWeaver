using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DBWeaver.UI.Services;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.ViewModels.Canvas;

/// <summary>
/// Manages the SQL execution preview panel.
/// Displays query results, execution diagnostics, error messages, loading states,
/// cancellation feedback, and elapsed-time metrics.
/// </summary>
public sealed class DataPreviewViewModel : ViewModelBase
{
    private readonly ILogger<DataPreviewViewModel> _logger;
    public event Action<string, string?>? ErrorNotified;

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
    private int? _executionTimeoutSeconds;

    public DataPreviewViewModel(ILogger<DataPreviewViewModel>? logger = null)
    {
        _logger = logger ?? NullLogger<DataPreviewViewModel>.Instance;
    }

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
            RaisePropertyChanged(nameof(IsNearTimeout));
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
            {
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(IsNearTimeout));
            }
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
            _logger.LogDebug(
                "ResultData set requested: old={OldRows} rows, new={NewRows} rows",
                _data?.Rows.Count ?? 0,
                value?.Rows.Count ?? 0
            );
            if (Set(ref _data, value))
            {
                RaisePropertyChanged(nameof(ResultView));
                RaisePropertyChanged(nameof(HasData));
                RaisePropertyChanged(nameof(StatusText));
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
                return null;

            var view = _data.DefaultView;
            return view;
        }
    }

    public bool HasData
    {
        get
        {
            return _data is { Rows.Count: > 0 };
        }
    }

    // ── Status text + color ───────────────────────────────────────────────────

    public string StatusText => _state switch
    {
        PreviewExecutionState.Running   => BuildRunningStatusText(),
        PreviewExecutionState.Cancelled => L("preview.status.cancelled", "Cancelled"),
        PreviewExecutionState.Failed    => L("preview.status.error", "Error"),
        PreviewExecutionState.Done      => $"{_rows} rows · {_ms}ms",
        _                               => L("preview.status.ready", "Ready"),
    };

    public string StatusColor => _state switch
    {
        PreviewExecutionState.Running   => UiColorConstants.C_60A5FA,
        PreviewExecutionState.Cancelled => UiColorConstants.C_FBBF24,
        PreviewExecutionState.Failed    => UiColorConstants.C_EF4444,
        PreviewExecutionState.Done      => UiColorConstants.C_4ADE80,
        _                               => UiColorConstants.C_4A5568,
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
    public string DiagnosticLabel => _diagnostic?.CategoryLabel ?? L("diagnostics.error", "Error");
    public string DiagnosticSuggestion => _diagnostic?.Suggestion ?? string.Empty;
    public string? DiagnosticTechnical => _diagnostic?.TechnicalDetail;
    public bool HasTechnicalDetail => !string.IsNullOrWhiteSpace(_diagnostic?.TechnicalDetail);

    // ── Public API (called by PreviewService) ─────────────────────────────────

    public void Toggle() => IsVisible = !IsVisible;

    /// <summary>Transitions to Running state; resets previous results.</summary>
    public void ShowLoading(string sql, int? timeoutSeconds = null)
    {
        _logger.LogDebug("ShowLoading called");
        QueryText = sql;
        _executionTimeoutSeconds = timeoutSeconds;
        RaisePropertyChanged(nameof(IsNearTimeout));
        CurrentState = PreviewExecutionState.Running;
        ErrorMessage = null;
        ResultData = null;
        Diagnostic = null;
        ElapsedMs = 0;
        IsVisible = true;
        _logger.LogDebug("Preview panel visible={IsVisible}", IsVisible);
    }

    /// <summary>Updates the live elapsed time counter shown in the status chip.</summary>
    public void UpdateElapsed(long ms)
    {
        ElapsedMs = ms;
    }

    /// <summary>Transitions to Done state and displays the result grid.</summary>
    public void ShowResults(DataTable dt, long ms)
    {
        _logger.LogDebug("ShowResults called with {Rows} rows in {ElapsedMs}ms", dt.Rows.Count, ms);

        ResultData = dt;

        RowCount = dt?.Rows.Count ?? 0;

        ExecutionMs = ms;
        ElapsedMs = ms;
        ErrorMessage = null;
        Diagnostic = null;

        CurrentState = PreviewExecutionState.Done;
        _logger.LogInformation("ShowResults completed: state={State}, rows={Rows}, elapsedMs={ElapsedMs}", CurrentState, RowCount, ms);
    }

    /// <summary>Transitions to Failed state with structured diagnostic.</summary>
    public void ShowError(string msg, Exception? ex = null)
    {
        Diagnostic = ErrorDiagnostics.Classify(msg, ex);
        ErrorMessage = Diagnostic.FriendlyMessage;
        CurrentState = PreviewExecutionState.Failed;
        ErrorNotified?.Invoke(ErrorMessage ?? msg, DiagnosticTechnical ?? ex?.ToString());
    }

    /// <summary>Transitions to Cancelled state; keeps previous result data visible.</summary>
    public void ShowCancelled()
    {
        ErrorMessage = null;
        Diagnostic = null;
        CurrentState = PreviewExecutionState.Cancelled;
    }

    public bool IsNearTimeout
    {
        get
        {
            if (_state != PreviewExecutionState.Running || !_executionTimeoutSeconds.HasValue || _executionTimeoutSeconds <= 0)
                return false;

            long timeoutMs = _executionTimeoutSeconds.Value * 1000L;
            return _elapsedMs >= (long)(timeoutMs * 0.8);
        }
    }

    private string BuildRunningStatusText()
    {
        if (!_executionTimeoutSeconds.HasValue || _executionTimeoutSeconds <= 0)
            return string.Format(L("preview.runningWithMs", "Running… {0}ms"), _elapsedMs);

        long timeoutMs = _executionTimeoutSeconds.Value * 1000L;
        if (!IsNearTimeout)
            return string.Format(
                L("preview.runningWithTimeout", "Running… {0}ms (timeout: {1}s)"),
                _elapsedMs,
                _executionTimeoutSeconds.Value
            );

        long remainingMs = Math.Max(0, timeoutMs - _elapsedMs);
        long remainingSec = (long)Math.Ceiling(remainingMs / 1000d);
        return string.Format(
            L(
                "preview.runningSlowWithTimeout",
                "Running… {0}ms (timeout: {1}s) · Slow query, timeout in {2}s"
            ),
            _elapsedMs,
            _executionTimeoutSeconds.Value,
            remainingSec
        );
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

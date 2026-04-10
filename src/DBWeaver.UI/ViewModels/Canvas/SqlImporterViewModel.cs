using System.Collections.ObjectModel;
using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.SqlImport;
using DBWeaver.UI.Services.SqlImport.Contracts;
using DBWeaver.UI.Services.SqlImport.Execution;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.SqlImport.Rewriting;
using DBWeaver.UI.Services.SqlImport.Validation;
using DBWeaver.UI.ViewModels.UndoRedo;


namespace DBWeaver.UI.ViewModels.Canvas;

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
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly ISqlImportExecutionService _executionService =
        new SqlImportExecutionService(
            canvas,
            new SqlImportSyntaxValidator(),
            new SqlImportCteRewriteService()
        );
    private CancellationTokenSource? _importCts;
    private bool _cancelRequestedByUser;

    private bool _isVisible;
    private bool _isImporting;
    private string _sqlInput = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _hasReport;
    private bool _isClearCanvasConfirmationVisible;
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

    public bool IsClearCanvasConfirmationVisible
    {
        get => _isClearCanvasConfirmationVisible;
        private set => Set(ref _isClearCanvasConfirmationVisible, value);
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
        IsClearCanvasConfirmationVisible = false;
        StatusMessage = string.Empty;
        IsVisible = true;
    }

    public void Close()
    {
        IsClearCanvasConfirmationVisible = false;
        IsVisible = false;
    }

    public async Task ConfirmClearCanvasAndImportAsync()
    {
        if (!IsClearCanvasConfirmationVisible || IsImporting)
            return;

        IsClearCanvasConfirmationVisible = false;
        await ImportAsync(forceCanvasClearConfirmation: true);
    }

    public void CancelClearCanvasConfirmation()
    {
        if (!IsClearCanvasConfirmationVisible || IsImporting)
            return;

        IsClearCanvasConfirmationVisible = false;
        StatusMessage = L(
            "sqlImporter.status.clearConfirmationCancelled",
            "Import cancelled. The current canvas was kept.");
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

    public async Task ImportAsync(bool forceCanvasClearConfirmation = false)
    {
        if (string.IsNullOrWhiteSpace(SqlInput))
        {
            StatusMessage = L("sqlImporter.status.pasteSelect", "Paste a SELECT statement above, then click Import.");
            return;
        }

        if (IsImporting)
            return;

        bool shouldAskClearConfirmation = !forceCanvasClearConfirmation && !_canvas.IsEmpty;
        if (shouldAskClearConfirmation)
        {
            IsClearCanvasConfirmationVisible = true;
            StatusMessage = L(
                "sqlImporter.status.clearConfirmationRequired",
                "Importing SQL will clear the current canvas. Confirm to continue.");
            return;
        }

        ClearTelemetry();
        IsClearCanvasConfirmationVisible = false;

        if (MaxSqlInputLength > 0 && SqlInput.Length > MaxSqlInputLength)
        {
            StatusMessage = string.Format(
                L("sqlImporter.status.inputTooLarge", "SQL input is too large ({0:N0} chars). Limit is {1:N0}. Split the query or increase the import limit."),
                SqlInput.Length,
                MaxSqlInputLength);
            Report.Clear();
            ResetReportTotals();
            HasReport = false;
            return;
        }
        IsImporting = true;
        _cancelRequestedByUser = false;
        StatusMessage = L("sqlImporter.status.parsing", "Parsing SQL...");
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

            SqlImportExecutionResult result = _executionService.Execute(sqlToImport, Report, token);
            ApplyTelemetry(result.Timing);
            ApplyReportTotals(result.Imported, result.Partial, result.Skipped);
            StatusMessage = string.Format(
                L("sqlImporter.status.done", "Done - {0} imported, {1} partial, {2} skipped."),
                result.Imported,
                result.Partial,
                result.Skipped);
            HasReport = true;

            // Capture post-import state so Redo can reapply import after Undo.
            stateBeforeImport.CaptureAfterState(_canvas);

            // Register restore command in undo stack to allow undoing the import
            // This lets users undo the import and go back to the pre-import state
            _canvas.UndoRedo.Execute(stateBeforeImport);

            if (result.Imported + result.Partial > 0)
                Close();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            StatusMessage =
                _cancelRequestedByUser
                    ? L("sqlImporter.status.cancelledByUser", "Import cancelled by user.")
                    : string.Format(
                        L("sqlImporter.status.timeout", "Import timed out after {0:0.#}s. Try a smaller query or increase timeout."),
                        ImportTimeout.TotalSeconds);
            HasReport = false;
            Report.Clear();
            ResetReportTotals();

            // On cancellation/timeout, restore pre-import canvas state immediately
            stateBeforeImport.Execute(_canvas);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(
                L("sqlImporter.status.parseError", "Parse error: {0}"),
                ex.Message);
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

    private void ClearTelemetry()
    {
        LastParseDurationMs = 0;
        LastMapDurationMs = 0;
        LastBuildDurationMs = 0;
        LastTotalDurationMs = 0;
    }

    private void ApplyTelemetry(SqlImportTiming timing)
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

    private string L(string key, string fallback)
    {
        string value = _loc[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

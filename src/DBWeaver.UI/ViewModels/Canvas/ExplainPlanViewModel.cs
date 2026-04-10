using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.ViewModels.Canvas;

// ── ViewModel ─────────────────────────────────────────────────────────────────

/// <summary>
/// Provides an in-panel EXPLAIN plan inspector.
/// Generates per-provider EXPLAIN SQL, parses the output into structured steps,
/// and highlights expensive operations (Seq Scan, Sort, Hash Join).
/// </summary>
public sealed class ExplainPlanViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;
    private readonly ILogger<ExplainPlanViewModel> _logger;
    private readonly IExplainExecutor _explainExecutor;
    private readonly IExplainNodeToStepMapper _nodeToStepMapper;
    private readonly IExplainCostDistributionCalculator _costDistributionCalculator;
    private readonly IExplainExecutionModeEvaluator _executionModeEvaluator;
    private readonly IExplainSimulationLatencyOptions _simulationLatencyOptions;
    private readonly IExplainSqlSafetyEvaluator _sqlSafetyEvaluator;
    private readonly IExplainSqlPreviewTextResolver _sqlPreviewTextResolver;
    private readonly IExplainHighlightedTableResolver _highlightedTableResolver;
    private readonly IExplainPlanExportFormatter _exportFormatter;
    private readonly IExplainDaliboUrlBuilder _daliboUrlBuilder;
    private readonly IExplainPlanComparisonBuilder _comparisonBuilder;
    private readonly IExplainIndexSuggestionEngine _indexSuggestionEngine;
    private readonly IExplainTreeLayoutBuilder _treeLayoutBuilder;
    private readonly PropertyChangedEventHandler _canvasPropertyChangedHandler;

    private bool _isVisible;
    private bool _isLoading;
    private bool _isSimulated = true;
    private bool _includeAnalyze;
    private bool _includeBuffers;
    private ExplainStep? _selectedStep;
    private string? _highlightedTableName;
    private string _rawOutput = "";
    private double? _planningTimeMs;
    private double? _executionTimeMs;
    private ExplainSnapshot? _selectedSnapshotA;
    private ExplainSnapshot? _selectedSnapshotB;
    private string? _selectedSuggestionSql;
    private bool _isTreeMode;
    private int _snapshotSequence;
    private int _openRequestToken;
    private string _sql = "";
    private DatabaseProvider _provider = DatabaseProvider.Postgres;
    private string? _errorMessage;
    private CancellationTokenSource? _runCancellationTokenSource;
    private int _runGeneration;

    public ExplainPlanViewModel(
        CanvasViewModel canvas,
        ILogger<ExplainPlanViewModel>? logger = null,
        IExplainExecutor? explainExecutor = null,
        IExplainNodeToStepMapper? nodeToStepMapper = null,
        IExplainCostDistributionCalculator? costDistributionCalculator = null,
        IExplainExecutionModeEvaluator? executionModeEvaluator = null,
        IExplainSimulationLatencyOptions? simulationLatencyOptions = null,
        IExplainSqlSafetyEvaluator? sqlSafetyEvaluator = null,
        IExplainSqlPreviewTextResolver? sqlPreviewTextResolver = null,
        IExplainHighlightedTableResolver? highlightedTableResolver = null,
        IExplainPlanExportFormatter? exportFormatter = null,
        IExplainDaliboUrlBuilder? daliboUrlBuilder = null,
        IExplainPlanComparisonBuilder? comparisonBuilder = null,
        IExplainIndexSuggestionEngine? indexSuggestionEngine = null,
        IExplainTreeLayoutBuilder? treeLayoutBuilder = null
    )
    {
        _canvas = canvas;
        _logger = logger ?? NullLogger<ExplainPlanViewModel>.Instance;
        _explainExecutor = explainExecutor ?? new ExplainExecutor();
        _nodeToStepMapper = nodeToStepMapper ?? new ExplainNodeToStepMapper();
        _costDistributionCalculator = costDistributionCalculator ?? new ExplainCostDistributionCalculator();
        _executionModeEvaluator = executionModeEvaluator ?? new ExplainExecutionModeEvaluator();
        _simulationLatencyOptions = simulationLatencyOptions ?? new ExplainSimulationLatencyOptions();
        _sqlSafetyEvaluator = sqlSafetyEvaluator ?? new ExplainSqlSafetyEvaluator();
        _sqlPreviewTextResolver = sqlPreviewTextResolver ?? new ExplainSqlPreviewTextResolver();
        _highlightedTableResolver = highlightedTableResolver ?? new ExplainHighlightedTableResolver();
        _exportFormatter = exportFormatter ?? new ExplainPlanExportFormatter();
        _daliboUrlBuilder = daliboUrlBuilder ?? new ExplainDaliboUrlBuilder();
        _comparisonBuilder = comparisonBuilder ?? new ExplainPlanComparisonBuilder();
        _indexSuggestionEngine = indexSuggestionEngine ?? new ExplainIndexSuggestionEngine();
        _treeLayoutBuilder = treeLayoutBuilder ?? new ExplainTreeLayoutBuilder();
        SelectStepCommand = new RelayCommand<ExplainStep>(SelectStep);
        CaptureSnapshotCommand = new RelayCommand(CaptureSnapshot, () => Steps.Count > 0);
        _canvasPropertyChangedHandler = OnCanvasPropertyChanged;
        _canvas.PropertyChanged += _canvasPropertyChangedHandler;
        UpdateSimulationFlag();
    }

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
        private set
        {
            if (!Set(ref _sql, value))
                return;

            RaisePropertyChanged(nameof(SqlTooltipText));
            RaisePropertyChanged(nameof(HasAnalyzeDmlWarning));
        }
    }

    public DatabaseProvider Provider
    {
        get => _provider;
        private set
        {
            Set(ref _provider, value);
            if (!CanUseBuffersOption && _includeBuffers)
                _includeBuffers = false;
            RaisePropertyChanged(nameof(ProviderLabel));
            RaisePropertyChanged(nameof(ExplainHeader));
            RaisePropertyChanged(nameof(CanUseAnalyzeOptions));
            RaisePropertyChanged(nameof(CanUseBuffersOption));
            RaisePropertyChanged(nameof(IncludeBuffers));
            RaisePropertyChanged(nameof(HasAnalyzeDmlWarning));
            RaisePropertyChanged(nameof(CanOpenDalibo));
            UpdateSimulationFlag();
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

    public bool IncludeAnalyze
    {
        get => _includeAnalyze;
        set
        {
            if (!Set(ref _includeAnalyze, value))
                return;

            if (!value && IncludeBuffers)
                IncludeBuffers = false;

            RaisePropertyChanged(nameof(HasAnalyzeDmlWarning));
        }
    }

    public bool IncludeBuffers
    {
        get => _includeBuffers;
        set => Set(ref _includeBuffers, value && IncludeAnalyze && CanUseBuffersOption);
    }

    public bool CanUseAnalyzeOptions =>
        Provider is DatabaseProvider.Postgres or DatabaseProvider.MySql or DatabaseProvider.SqlServer;
    public bool CanUseBuffersOption => Provider == DatabaseProvider.Postgres;
    public bool HasAnalyzeDmlWarning =>
        IncludeAnalyze && CanUseAnalyzeOptions && _sqlSafetyEvaluator.LooksMutating(Sql);
    public string AnalyzeDmlWarningText => L(
        "explain.analyzeDmlWarning",
        "Analyze executes the query. This SQL appears to be data-mutating (INSERT/UPDATE/DELETE/ALTER/DROP/TRUNCATE)."
    );

    public bool IsSimulated
    {
        get => _isSimulated;
        private set => Set(ref _isSimulated, value);
    }
    public string SqlTooltipText => _sqlPreviewTextResolver.Resolve(_sql);
    public string? HighlightedTableName
    {
        get => _highlightedTableName;
        private set => Set(ref _highlightedTableName, value);
    }
    public string RawOutput
    {
        get => _rawOutput;
        private set
        {
            if (!Set(ref _rawOutput, value))
                return;

            RaisePropertyChanged(nameof(HasRawOutput));
            RaisePropertyChanged(nameof(CanOpenDalibo));
        }
    }
    public bool HasRawOutput => !string.IsNullOrWhiteSpace(_rawOutput);
    public bool CanOpenDalibo => Provider == DatabaseProvider.Postgres && !string.IsNullOrWhiteSpace(BuildDaliboUrl());

    // ── Results ───────────────────────────────────────────────────────────────

    public ObservableCollection<ExplainStep> Steps { get; } = [];
    public ObservableCollection<ExplainSnapshot> Snapshots { get; } = [];
    public ObservableCollection<ExplainComparisonRow> ComparisonRows { get; } = [];
    public ObservableCollection<ExplainIndexSuggestion> IndexSuggestions { get; } = [];
    public ObservableCollection<ExplainHistoryItem> History { get; } = [];
    public ObservableCollection<ExplainTreeVisualNode> TreeNodes { get; } = [];
    public ObservableCollection<ExplainTreeVisualEdge> TreeEdges { get; } = [];

    public bool HasData => Steps.Count > 0 && !_isLoading;
    public ExplainStep? SelectedStep
    {
        get => _selectedStep;
        private set
        {
            if (!Set(ref _selectedStep, value))
                return;

            RaisePropertyChanged(nameof(HasSelectedStep));
            RaisePropertyChanged(nameof(SelectedStepTitle));
            RaisePropertyChanged(nameof(SelectedStepDetailText));
            RaisePropertyChanged(nameof(SelectedStepEstimatedRowsText));
            RaisePropertyChanged(nameof(SelectedStepActualRowsText));
            RaisePropertyChanged(nameof(SelectedStepRowsErrorText));
            RaisePropertyChanged(nameof(SelectedStepActualTimeText));
            RaisePropertyChanged(nameof(SelectedStepLoopsText));
            RaisePropertyChanged(nameof(SelectedStepSuggestionText));
        }
    }
    public bool HasSelectedStep => SelectedStep is not null;
    public string SelectedStepTitle => SelectedStep?.Operation ?? string.Empty;
    public string SelectedStepDetailText => SelectedStep?.Detail ?? "No details available.";
    public string SelectedStepEstimatedRowsText =>
        SelectedStep?.EstimatedRows.HasValue == true
            ? SelectedStep.EstimatedRows.Value.ToString("N0", CultureInfo.InvariantCulture)
            : "–";
    public string SelectedStepActualRowsText =>
        SelectedStep?.ActualRows.HasValue == true
            ? SelectedStep.ActualRows.Value.ToString("N0", CultureInfo.InvariantCulture)
            : "–";
    public string SelectedStepRowsErrorText => SelectedStep?.RowsErrorText ?? "–";
    public string SelectedStepActualTimeText => SelectedStep?.ActualTimeText ?? "–";
    public string SelectedStepLoopsText => SelectedStep?.ActualLoopsText ?? "–";
    public string SelectedStepSuggestionText =>
        SelectedStep?.IsStaleStats == true
            ? "High estimate mismatch detected. Refresh table statistics (ANALYZE/UPDATE STATISTICS)."
            : "No optimization hint for this step.";
    public string PlanningTimeText => _planningTimeMs.HasValue
        ? _planningTimeMs.Value.ToString("0.###", CultureInfo.InvariantCulture) + " ms"
        : "–";
    public string ExecutionTimeText => _executionTimeMs.HasValue ? $"{_executionTimeMs.Value:0.###} ms" : "–";
    public bool HasTimingData => _planningTimeMs.HasValue || _executionTimeMs.HasValue;

    public int AlertCount => Steps.Count(s => s.IsExpensive);
    public bool HasAlerts => AlertCount > 0;
    public string AlertSummaryText
    {
        get
        {
            if (AlertCount <= 0)
                return string.Empty;

            IReadOnlyList<string> topOperations = Steps
                .Where(s => s.IsExpensive)
                .Select(s => s.Operation)
                .Where(op => !string.IsNullOrWhiteSpace(op))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            string operationSummary = string.Join(", ", topOperations);
            return string.IsNullOrWhiteSpace(operationSummary)
                ? $"{AlertCount} operação(ões) cara(s) detectada(s)."
                : $"{AlertCount} operação(ões) cara(s) detectada(s): {operationSummary}.";
        }
    }
    public ICommand SelectStepCommand { get; }
    public RelayCommand CaptureSnapshotCommand { get; }
    public ExplainSnapshot? SelectedSnapshotA
    {
        get => _selectedSnapshotA;
        set
        {
            if (!Set(ref _selectedSnapshotA, value))
                return;

            RebuildComparisonRows();
        }
    }
    public ExplainSnapshot? SelectedSnapshotB
    {
        get => _selectedSnapshotB;
        set
        {
            if (!Set(ref _selectedSnapshotB, value))
                return;

            RebuildComparisonRows();
        }
    }
    public bool CanCompareSnapshots => Snapshots.Count >= 2;
    public bool HasComparisonRows => ComparisonRows.Count > 0;
    public bool HasIndexSuggestions => IndexSuggestions.Count > 0;
    public bool HasHistory => History.Count > 0;
    public bool IsTreeMode
    {
        get => _isTreeMode;
        set
        {
            if (!Set(ref _isTreeMode, value))
                return;

            RaisePropertyChanged(nameof(IsListMode));
            RaisePropertyChanged(nameof(ShowListView));
            RaisePropertyChanged(nameof(ShowTreeView));
        }
    }
    public bool IsListMode => !IsTreeMode;
    public bool ShowListView => HasData && !IsTreeMode;
    public bool ShowTreeView => HasData && IsTreeMode;
    public double TreeCanvasWidth { get; private set; }
    public double TreeCanvasHeight { get; private set; }
    public string? SelectedSuggestionSql
    {
        get => _selectedSuggestionSql;
        private set => Set(ref _selectedSuggestionSql, value);
    }
    public int OpenRequestToken
    {
        get => _openRequestToken;
        private set => Set(ref _openRequestToken, value);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        Sql      = _canvas.LiveSql.RawSql;
        Provider = _canvas.LiveSql.Provider;
        IsVisible = true;
        OpenRequestToken++;
        UpdateSimulationFlag();

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
            ErrorMessage = string.Format(
                L("explain.errorWithReason", "Explain plan error: {0}"),
                ex.Message
            );
            IsLoading = false;
            _logger.LogError(ex, "Unhandled exception in fire-and-forget explain plan run");
        }
    }

    public void Close()
    {
        CancelActiveRun();
        IsVisible = false;
        SelectedStep = null;
        HighlightedTableName = null;
        SelectedSnapshotA = null;
        SelectedSnapshotB = null;
        IndexSuggestions.Clear();
        SelectedSuggestionSql = null;
        _planningTimeMs = null;
        _executionTimeMs = null;
        TreeNodes.Clear();
        TreeEdges.Clear();
        TreeCanvasWidth = 0;
        TreeCanvasHeight = 0;
        RaisePropertyChanged(nameof(HasHistory));
        RaisePropertyChanged(nameof(PlanningTimeText));
        RaisePropertyChanged(nameof(ExecutionTimeText));
        RaisePropertyChanged(nameof(HasTimingData));
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    public async Task RunExplainAsync()
    {
        int runGeneration = BeginNewRun();
        CancellationToken runCancellationToken = _runCancellationTokenSource?.Token ?? CancellationToken.None;

        // Refresh in case SQL/provider changed since Open() or previous run.
        Sql = _canvas.LiveSql.RawSql;
        Provider = _canvas.LiveSql.Provider;

        if (string.IsNullOrWhiteSpace(_sql))
        {
            ErrorMessage = L("explain.noSql", "No SQL to explain. Build a query on the canvas first.");
            if (runGeneration == _runGeneration)
            {
                IsLoading = false;
                DisposeRunCancellationTokenSource();
            }
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        Steps.Clear();
        SelectedStep = null;
        HighlightedTableName = null;
        RawOutput = string.Empty;
        _planningTimeMs = null;
        _executionTimeMs = null;
        IndexSuggestions.Clear();
        SelectedSuggestionSql = null;
        TreeNodes.Clear();
        TreeEdges.Clear();
        TreeCanvasWidth = 0;
        TreeCanvasHeight = 0;
        RaisePropertyChanged(nameof(HasData));
        RaisePropertyChanged(nameof(AlertCount));
        RaisePropertyChanged(nameof(HasAlerts));
        RaisePropertyChanged(nameof(AlertSummaryText));
        RaisePropertyChanged(nameof(HasIndexSuggestions));
        RaisePropertyChanged(nameof(HasHistory));
        RaisePropertyChanged(nameof(PlanningTimeText));
        RaisePropertyChanged(nameof(ExecutionTimeText));
        RaisePropertyChanged(nameof(HasTimingData));
        RaisePropertyChanged(nameof(ShowListView));
        RaisePropertyChanged(nameof(ShowTreeView));
        RaisePropertyChanged(nameof(TreeCanvasWidth));
        RaisePropertyChanged(nameof(TreeCanvasHeight));
        CaptureSnapshotCommand.NotifyCanExecuteChanged();

        if (IncludeAnalyze && CanUseAnalyzeOptions && _sqlSafetyEvaluator.LooksMutating(Sql))
        {
            ErrorMessage = AnalyzeDmlWarningText;
            if (runGeneration == _runGeneration)
            {
                IsLoading = false;
                DisposeRunCancellationTokenSource();
            }
            return;
        }

        try
        {
            int simulatedDelayMs = _simulationLatencyOptions.ResolveDelayMs();
            if (simulatedDelayMs > 0)
                await Task.Delay(simulatedDelayMs, runCancellationToken);

            ExplainResult result = await _explainExecutor.RunAsync(
                _sql,
                _provider,
                _canvas.ActiveConnectionConfig,
                new ExplainOptions(
                    IncludeAnalyze: IncludeAnalyze,
                    IncludeBuffers: IncludeBuffers,
                    Format: ExplainFormat.Json
                ),
                runCancellationToken
            );
            IsSimulated = result.IsSimulated;
            RawOutput = result.RawOutput;
            _planningTimeMs = result.PlanningTimeMs;
            _executionTimeMs = result.ExecutionTimeMs;
            RaisePropertyChanged(nameof(PlanningTimeText));
            RaisePropertyChanged(nameof(ExecutionTimeText));
            RaisePropertyChanged(nameof(HasTimingData));
            IReadOnlyList<ExplainStep> steps = _nodeToStepMapper.Map(result.Nodes);
            _costDistributionCalculator.Apply(steps);
            IReadOnlyList<ExplainIndexSuggestion> suggestions = _indexSuggestionEngine.Build(steps, _provider);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (ExplainStep s in steps)
                    Steps.Add(s);
                IndexSuggestions.Clear();
                foreach (ExplainIndexSuggestion suggestion in suggestions)
                    IndexSuggestions.Add(suggestion);

                RaisePropertyChanged(nameof(HasData));
                RaisePropertyChanged(nameof(AlertCount));
                RaisePropertyChanged(nameof(HasAlerts));
                RaisePropertyChanged(nameof(AlertSummaryText));
                RaisePropertyChanged(nameof(HasIndexSuggestions));
                CaptureSnapshotCommand.NotifyCanExecuteChanged();
                AppendHistoryEntry(steps);
                RefreshTreeLayout(steps);
                RaisePropertyChanged(nameof(ShowListView));
                RaisePropertyChanged(nameof(ShowTreeView));
            });
        }
        catch (OperationCanceledException) when (runCancellationToken.IsCancellationRequested)
        {
            // Run was cancelled by close/re-run; do not surface as error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate explain plan");
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (runGeneration == _runGeneration)
            {
                IsLoading = false;
                DisposeRunCancellationTokenSource();
            }
        }
    }

    private int BeginNewRun()
    {
        CancelActiveRun();
        _runCancellationTokenSource = new CancellationTokenSource();
        return ++_runGeneration;
    }

    private void CancelActiveRun()
    {
        if (_runCancellationTokenSource is null)
            return;

        try
        {
            _runCancellationTokenSource.Cancel();
        }
        catch
        {
        }

        DisposeRunCancellationTokenSource();
    }

    private void DisposeRunCancellationTokenSource()
    {
        _runCancellationTokenSource?.Dispose();
        _runCancellationTokenSource = null;
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private void OnCanvasPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasViewModel.ActiveConnectionConfig))
            UpdateSimulationFlag();
    }

    private void UpdateSimulationFlag()
    {
        DatabaseProvider provider = _canvas.ActiveConnectionConfig?.Provider ?? _canvas.LiveSql.Provider;
        IsSimulated = _executionModeEvaluator.IsSimulated(provider, _canvas.ActiveConnectionConfig);
    }

    private void SelectStep(ExplainStep? step)
    {
        if (step is null)
            return;

        SelectedStep = step;
        HighlightedTableName = _highlightedTableResolver.Resolve(step);
    }

    public string BuildExportText()
    {
        return _exportFormatter.Format(
            new ExplainPlanExportData(
                ProviderLabel: ProviderLabel,
                Sql: Sql,
                Steps: Steps.ToList(),
                PlanningTimeMs: _planningTimeMs,
                ExecutionTimeMs: _executionTimeMs,
                GeneratedAtUtc: DateTimeOffset.UtcNow
            )
        );
    }

    public string? BuildDaliboUrl() => _daliboUrlBuilder.Build(_rawOutput);

    public void SelectIndexSuggestion(ExplainIndexSuggestion? suggestion)
    {
        if (suggestion is null)
            return;

        SelectedSuggestionSql = suggestion.Sql;
    }

    public void SetListMode() => IsTreeMode = false;

    public void SetTreeMode() => IsTreeMode = true;

    public IReadOnlyList<ExplainHistoryState> ExportHistoryState() =>
        History.Select(h => h.ToState()).ToList();

    public void ImportHistoryState(IReadOnlyList<ExplainHistoryState>? states)
    {
        History.Clear();
        if (states is not null)
        {
            foreach (ExplainHistoryState state in states.TakeLast(10))
                History.Add(ExplainHistoryItem.FromState(state));
        }

        RaisePropertyChanged(nameof(HasHistory));
    }

    public void CaptureSnapshot()
    {
        if (Steps.Count == 0)
            return;

        var clonedSteps = Steps
            .Select(s =>
                new ExplainStep
                {
                    NodeId = s.NodeId,
                    ParentNodeId = s.ParentNodeId,
                    StepNumber = s.StepNumber,
                    Operation = s.Operation,
                    Detail = s.Detail,
                    RelationName = s.RelationName,
                    IndexName = s.IndexName,
                    Predicate = s.Predicate,
                    StartupCost = s.StartupCost,
                    EstimatedCost = s.EstimatedCost,
                    EstimatedRows = s.EstimatedRows,
                    ActualStartupTimeMs = s.ActualStartupTimeMs,
                    ActualTotalTimeMs = s.ActualTotalTimeMs,
                    ActualLoops = s.ActualLoops,
                    ActualRows = s.ActualRows,
                    IndentLevel = s.IndentLevel,
                    IsExpensive = s.IsExpensive,
                    AlertLabel = s.AlertLabel,
                    CostFraction = s.CostFraction,
                }
            )
            .ToList();

        _snapshotSequence++;
        var snapshot = new ExplainSnapshot(
            Label: $"Snapshot {_snapshotSequence}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Steps: clonedSteps
        );

        Snapshots.Add(snapshot);
        while (Snapshots.Count > 5)
            Snapshots.RemoveAt(0);

        SelectedSnapshotB = Snapshots.LastOrDefault();
        if (SelectedSnapshotA is null || ReferenceEquals(SelectedSnapshotA, SelectedSnapshotB))
            SelectedSnapshotA = Snapshots.Count >= 2 ? Snapshots[^2] : null;

        RaisePropertyChanged(nameof(CanCompareSnapshots));
        RebuildComparisonRows();
    }

    private void RebuildComparisonRows()
    {
        ComparisonRows.Clear();
        if (SelectedSnapshotA is null || SelectedSnapshotB is null)
        {
            RaisePropertyChanged(nameof(HasComparisonRows));
            return;
        }

        IReadOnlyList<ExplainComparisonRow> rows = _comparisonBuilder.Build(SelectedSnapshotA, SelectedSnapshotB);
        foreach (ExplainComparisonRow row in rows)
            ComparisonRows.Add(row);

        RaisePropertyChanged(nameof(HasComparisonRows));
    }

    private void AppendHistoryEntry(IReadOnlyList<ExplainStep> steps)
    {
        string topOperation = steps.FirstOrDefault()?.Operation ?? "Plan";
        double? topCost = steps.FirstOrDefault()?.EstimatedCost;
        int alerts = steps.Count(s => s.IsExpensive);

        History.Insert(
            0,
            new ExplainHistoryItem
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                TopOperation = topOperation,
                TopCost = topCost,
                AlertCount = alerts,
            }
        );

        while (History.Count > 10)
            History.RemoveAt(History.Count - 1);

        RaisePropertyChanged(nameof(HasHistory));
    }

    public void RefreshTreeLayout()
    {
        RefreshTreeLayout(Steps.ToList());
    }

    private void RefreshTreeLayout(IReadOnlyList<ExplainStep> steps)
    {
        ExplainTreeLayoutResult layout = _treeLayoutBuilder.Build(steps);

        TreeNodes.Clear();
        foreach (ExplainTreeVisualNode node in layout.Nodes)
            TreeNodes.Add(node);

        TreeEdges.Clear();
        foreach (ExplainTreeVisualEdge edge in layout.Edges)
            TreeEdges.Add(edge);

        TreeCanvasWidth = layout.CanvasWidth;
        TreeCanvasHeight = layout.CanvasHeight;
        RaisePropertyChanged(nameof(TreeCanvasWidth));
        RaisePropertyChanged(nameof(TreeCanvasHeight));
    }
}

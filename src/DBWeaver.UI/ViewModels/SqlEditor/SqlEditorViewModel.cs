using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Benchmark;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.Settings;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;
using System.Diagnostics;
using System.Data;
using System.IO;
using System.Windows.Input;

namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorViewModel : ViewModelBase
{
    private const double MinResultsSheetHeight = 160;
    private const double MaxResultsSheetHeight = 720;
    private const int DraftMinimumTextLength = 5;
    private static readonly TimeSpan DraftAutoSaveDebounce = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DraftAutoSaveInterval = TimeSpan.FromSeconds(60);

    private readonly SqlSelectionExtractor _selectionExtractor;
    private readonly SqlScriptStatementSplitter _statementSplitter;
    private readonly SqlEditorExecutionService _executionService;
    private readonly SqlEditorExecutionFeedbackService _executionFeedbackService;
    private readonly SqlEditorFileService _fileService;
    private readonly SqlEditorMutationExecutionOrchestrator _mutationExecutionOrchestrator;
    private readonly SqlEditorCommandNotifier _commandNotifier;
    private readonly SqlEditorPropertyChangePublisher _propertyChangePublisher;
    private readonly SqlEditorResultStateService _resultStateService;
    private readonly SqlEditorTabCloseWorkflowService _tabCloseWorkflowService;
    private readonly ISqlEditorSessionDraftStore _sessionDraftStore;
    private readonly ILocalizationService _localization;
    private readonly Func<ConnectionConfig?> _connectionConfigResolver;
    private readonly Func<string?, ConnectionConfig?> _connectionConfigByProfileIdResolver;
    private readonly Func<IReadOnlyList<SqlEditorConnectionProfileOption>> _connectionProfilesResolver;
    private readonly Func<ConnectionManagerViewModel?> _sharedConnectionManagerResolver;
    private readonly SqlCompletionProvider _completionProvider;
    private readonly Func<DbMetadata?> _metadataResolver;
    private readonly IExplainExecutor _explainExecutor;
    private CancellationTokenSource? _executionCts;
    private CancellationTokenSource? _benchmarkCts;
    private CancellationTokenSource? _explainCts;
    private bool _isExecuting;
    private bool _hasExecutionError;
    private string _executionStatusText;
    private string? _executionDetailText;
    private string _historySearchText = string.Empty;
    private bool _isHistoryClearConfirmationPending;
    private bool _isResultsSheetOpen;
    private double _resultsSheetHeight;
    private double _preferredResultsSheetHeight;
    private MutationGuardResult? _pendingMutationGuard;
    private SqlMutationDiffPreview? _pendingMutationDiff;
    private string? _pendingMutationSql;
    private long? _pendingMutationEstimatedRows;
    private bool _isCancellationPending;
    private Timer? _executionStatusTimer;
    private Stopwatch? _executionStopwatch;
    private int _activeExecutionTotalStatements;
    private int _activeExecutionStatementIndex;
    private int _activeExecutionStatementStartLine;
    private int _activeExecutionStatementEndLine;
    private int _cursorLine = 1;
    private int _cursorColumn = 1;
    private bool _isExplainRunning;
    private string _explainSummaryText = string.Empty;
    private string _explainRawOutput = string.Empty;
    private bool _isBenchmarkRunning;
    private string _benchmarkProgressText = string.Empty;
    private string _benchmarkSummaryText = string.Empty;
    private BenchmarkRunResult? _latestBenchmarkResult;
    private Timer? _draftAutoSaveDebounceTimer;
    private Timer? _draftAutoSaveForcedTimer;
    private bool _hasPendingDraftAutoSave;
    private bool _draftAutoSaveTimersStarted;

    public SqlEditorViewModel(
        DatabaseProvider initialProvider = DatabaseProvider.Postgres,
        string? initialConnectionProfileId = null,
        SqlSelectionExtractor? selectionExtractor = null,
        SqlScriptStatementSplitter? statementSplitter = null,
        SqlEditorExecutionService? executionService = null,
        SqlEditorMutationExecutionOrchestrator? mutationExecutionOrchestrator = null,
        ILocalizationService? localization = null,
        Func<ConnectionConfig?>? connectionConfigResolver = null,
        Func<string?, ConnectionConfig?>? connectionConfigByProfileIdResolver = null,
        Func<IReadOnlyList<SqlEditorConnectionProfileOption>>? connectionProfilesResolver = null,
        SqlCompletionProvider? completionProvider = null,
        Func<DbMetadata?>? metadataResolver = null,
        Func<ConnectionManagerViewModel?>? sharedConnectionManagerResolver = null,
        ISqlEditorSessionDraftStore? sessionDraftStore = null)
    {
        _localization = localization ?? LocalizationService.Instance;
        _selectionExtractor = selectionExtractor ?? new SqlSelectionExtractor();
        _statementSplitter = statementSplitter ?? new SqlScriptStatementSplitter();
        _executionService = executionService ?? new SqlEditorExecutionService(localization: _localization);
        _executionFeedbackService = new SqlEditorExecutionFeedbackService(_localization);
        _fileService = new SqlEditorFileService(_localization);
        MutationGuardService resolvedMutationGuardService = new(_localization);
        SqlMutationDiffService resolvedMutationDiffService = new(_executionService, _localization);
        _mutationExecutionOrchestrator = mutationExecutionOrchestrator ?? new SqlEditorMutationExecutionOrchestrator(
            _executionService,
            resolvedMutationGuardService,
            resolvedMutationDiffService,
            _localization);
        _resultStateService = new SqlEditorResultStateService(_localization);
        _commandNotifier = new SqlEditorCommandNotifier();
        _propertyChangePublisher = new SqlEditorPropertyChangePublisher();
        _tabCloseWorkflowService = new SqlEditorTabCloseWorkflowService(_localization);
        _sessionDraftStore = sessionDraftStore ?? new SqlEditorSessionDraftStore();
        _connectionConfigResolver = connectionConfigResolver ?? (() => null);
        _connectionConfigByProfileIdResolver = connectionConfigByProfileIdResolver ?? (_ => _connectionConfigResolver());
        _connectionProfilesResolver = connectionProfilesResolver ?? (() => []);
        _sharedConnectionManagerResolver = sharedConnectionManagerResolver ?? (() => null);
        _completionProvider = completionProvider ?? new SqlCompletionProvider();
        _metadataResolver = metadataResolver ?? (() => null);
        _explainExecutor = new ExplainExecutor();
        _executionStatusText = L("sqlEditor.status.ready", "Pronto.");
        _preferredResultsSheetHeight = ClampResultsSheetHeight(AppSettingsStore.LoadSqlEditorResultsSheetHeight());
        Tabs = new SqlEditorTabManagerViewModel(_localization);
        RestoreOrInitializeTabs(initialProvider, initialConnectionProfileId);
        TryHydrateResultFilterForTab(Tabs.GetActiveTab(), force: true);
        TryHydrateExecutionHistoryForTab(Tabs.GetActiveTab(), force: true);
        InitializeDraftAutoSaveTimers();
        NewTabCommand = new RelayCommand(
            AddNewTab,
            () => !IsExecuting);
        CloseTabCommand = new RelayCommand<string>(
            CloseTabById,
            tabId => !IsExecuting && CanCloseTab(tabId));
        CloseActiveTabCommand = new RelayCommand(
            () => CloseTabById(ActiveTab.Id),
            () => !IsExecuting && CanCloseTab(ActiveTab.Id));
        ConfirmPendingCloseTabCommand = new RelayCommand(
            ConfirmPendingCloseTab,
            () => !IsExecuting && HasPendingCloseTabConfirmation);
        CancelPendingCloseTabCommand = new RelayCommand(
            CancelPendingCloseTab,
            () => !IsExecuting && HasPendingCloseTabConfirmation);
        ConfirmPendingMutationCommand = new RelayCommand(
            () => _ = ConfirmPendingMutationAsync(),
            () => HasPendingMutationConfirmation && !IsExecuting);
        CancelPendingMutationCommand = new RelayCommand(
            CancelPendingMutation,
            () => HasPendingMutationConfirmation && !IsExecuting);
        OpenConnectionSwitcherCommand = new RelayCommand(
            OpenConnectionSwitcher,
            () => AvailableConnectionProfiles.Count > 0);
        ExecuteHistoryEntryCommand = new RelayCommand<SqlEditorHistoryEntry>(
            entry => _ = ExecuteHistoryEntryAsync(entry),
            entry => !IsExecuting && entry is not null && !string.IsNullOrWhiteSpace(entry.Sql));
        CloseResultsSheetCommand = new RelayCommand(
            CloseResultsSheet,
            () => IsResultsSheetOpen);
        SyncTabCommands();
    }

    public SqlEditorTabManagerViewModel Tabs { get; }

    public SqlEditorTabState ActiveTab => Tabs.GetActiveTab();
    public IReadOnlyList<SqlEditorTabState> EditorTabs => Tabs.Tabs;
    public int ActiveEditorTabIndex
    {
        get => Tabs.ActiveTabIndex;
        set
        {
            if (Tabs.TryActivate(value))
                RaiseTabStateChanged();
        }
    }
    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand CloseActiveTabCommand { get; }
    public ICommand ConfirmPendingCloseTabCommand { get; }
    public ICommand CancelPendingCloseTabCommand { get; }
    public ICommand ConfirmPendingMutationCommand { get; }
    public ICommand CancelPendingMutationCommand { get; }
    public ICommand OpenConnectionSwitcherCommand { get; }
    public ICommand ExecuteHistoryEntryCommand { get; }
    public ICommand CloseResultsSheetCommand { get; }
    public IReadOnlyList<DatabaseProvider> AvailableProviders { get; } = Enum.GetValues<DatabaseProvider>();
    public DatabaseProvider ActiveTabProvider
    {
        get => ResolveConnectionConfigForActiveTab()?.Provider ?? ActiveTab.Provider;
        set
        {
            if (HasResolvedConnection)
                return;

            if (ActiveTab.Provider == value)
                return;

            ActiveTab.Provider = value;
            if (!string.IsNullOrWhiteSpace(ActiveTab.ConnectionProfileId))
            {
                bool hasCompatibleProfile = AvailableConnectionProfiles.Any(p =>
                    string.Equals(p.Id, ActiveTab.ConnectionProfileId, StringComparison.Ordinal));
                if (!hasCompatibleProfile)
                    ActiveTab.ConnectionProfileId = null;
            }

            RaiseSqlPanelPropertiesChanged();
        }
    }
    public IReadOnlyList<SqlEditorConnectionProfileOption> AvailableConnectionProfiles =>
        _connectionProfilesResolver();
    public SqlEditorConnectionProfileOption? ActiveTabConnectionProfile
    {
        get => AvailableConnectionProfiles.FirstOrDefault(option =>
            string.Equals(option.Id, ActiveTab.ConnectionProfileId, StringComparison.Ordinal));
        set => ActiveTabConnectionProfileId = value?.Id;
    }
    public string? ActiveTabConnectionProfileId
    {
        get => ActiveTab.ConnectionProfileId;
        set
        {
            if (string.Equals(ActiveTab.ConnectionProfileId, value, StringComparison.Ordinal))
                return;

            bool hasValue = !string.IsNullOrWhiteSpace(value);
            bool existsInAnyProfile = hasValue && AvailableConnectionProfiles.Any(p =>
                string.Equals(p.Id, value, StringComparison.Ordinal));
            ActiveTab.ConnectionProfileId = existsInAnyProfile ? value : null;
            if (existsInAnyProfile)
            {
                SqlEditorConnectionProfileOption? selected = AvailableConnectionProfiles.FirstOrDefault(p =>
                    string.Equals(p.Id, value, StringComparison.Ordinal));
                if (selected is not null)
                    ActiveTab.Provider = selected.Provider;
            }

            HistorySearchText = string.Empty;
            TryHydrateExecutionHistoryForTab(ActiveTab, force: true);
            RaiseSqlPanelPropertiesChanged();
        }
    }
    public bool HasConnectionProfiles => AvailableConnectionProfiles.Count > 0;
    public ConnectionManagerViewModel? SharedConnectionManager => _sharedConnectionManagerResolver();
    public bool HasSharedConnectionManager => SharedConnectionManager is not null;
    public bool HasResolvedConnection => ResolveConnectionConfigForActiveTab() is not null;
    public string ActiveConnectionDisplayName
    {
        get
        {
            SqlEditorConnectionProfileOption? profile = ActiveTabConnectionProfile;
            if (profile is not null)
                return profile.DisplayName;

            ConnectionConfig? config = ResolveConnectionConfigForActiveTab();
            if (config is null)
                return L("sqlEditor.connection.none", "Sem conexao ativa.");

            return $"{config.Database} @ {config.Host}:{config.Port}";
        }
    }
    public string ActiveConnectionSubtitle
    {
        get
        {
            ConnectionConfig? config = ResolveConnectionConfigForActiveTab();
            if (config is null)
                return L("sqlEditor.connection.required", "Conecte-se a um banco para executar e inspecionar o schema.");

            return $"{config.Provider}  •  {config.Username}";
        }
    }
    public bool ShowDialectSelector => !HasResolvedConnection;
    public DatabaseProvider FallbackDialect
    {
        get => ActiveTab.Provider;
        set
        {
            if (ActiveTab.Provider == value)
                return;

            ActiveTab.Provider = value;
            RaiseSqlPanelPropertiesChanged();
        }
    }
    public bool IsResultsSheetOpen
    {
        get => _isResultsSheetOpen;
        private set
        {
            if (!Set(ref _isResultsSheetOpen, value))
                return;

            ResultsSheetHeight = value ? _preferredResultsSheetHeight : 0;
            RaisePropertyChanged(nameof(ShouldShowResultsSheet));
            NotifyCommands();
        }
    }

    public bool ShouldShowResultsSheet =>
        IsResultsSheetOpen
        && CurrentResult is not null
        && ((CurrentResult.Data?.Rows.Count ?? 0) > 0 || !string.IsNullOrWhiteSpace(CurrentResult.ErrorMessage));

    public double ResultsSheetHeight
    {
        get => _resultsSheetHeight;
        private set => Set(ref _resultsSheetHeight, value);
    }

    public void SetResultsSheetHeight(double height)
    {
        if (!IsResultsSheetOpen)
            return;

        double bounded = ClampResultsSheetHeight(height);
        ResultsSheetHeight = bounded;
        _preferredResultsSheetHeight = bounded;
    }

    public void PersistResultsSheetHeightPreference()
    {
        AppSettingsStore.SaveSqlEditorResultsSheetHeight(_preferredResultsSheetHeight);
    }

    public bool IsResultColumnHidden(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return false;

        return ActiveTab.HiddenResultColumns.Contains(columnName);
    }

    public void HideResultColumn(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return;

        if (ActiveTab.HiddenResultColumns.Contains(columnName))
            return;

        ActiveTab.HiddenResultColumns.Add(columnName);
        ActiveTab.HiddenResultColumnsHistory.Add(columnName);
        RaiseSqlPanelPropertiesChanged();
    }

    public void ShowAllResultColumns()
    {
        if (ActiveTab.HiddenResultColumns.Count == 0)
            return;

        ActiveTab.HiddenResultColumns.Clear();
        ActiveTab.HiddenResultColumnsHistory.Clear();
        RaiseSqlPanelPropertiesChanged();
    }

    public bool HasHiddenResultColumns => ActiveTab.HiddenResultColumns.Count > 0;
    public int HiddenResultColumnsCount => ActiveTab.HiddenResultColumns.Count;
    public string ShowAllColumnsButtonText => string.Format(
        L("sqlEditor.results.showAllColumns", "Mostrar colunas ({0})"),
        HiddenResultColumnsCount);
    public bool HasHiddenColumnUndo => ActiveTab.HiddenResultColumnsHistory.Count > 0;
    public string UndoHiddenColumnButtonText => L("sqlEditor.results.undoHidden", "Desfazer ocultar");

    public bool UndoLastHiddenResultColumn()
    {
        if (!HasHiddenColumnUndo)
            return false;

        for (int i = ActiveTab.HiddenResultColumnsHistory.Count - 1; i >= 0; i--)
        {
            string columnName = ActiveTab.HiddenResultColumnsHistory[i];
            ActiveTab.HiddenResultColumnsHistory.RemoveAt(i);
            if (!ActiveTab.HiddenResultColumns.Contains(columnName))
                continue;

            ActiveTab.HiddenResultColumns.Remove(columnName);
            RaiseSqlPanelPropertiesChanged();
            return true;
        }

        RaiseSqlPanelPropertiesChanged();
        return false;
    }

    public string ResultGridFilterText
    {
        get => ActiveTab.ResultGridFilterText;
        set
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(ActiveTab.ResultGridFilterText, normalized, StringComparison.Ordinal))
                return;

            ActiveTab.ResultGridFilterText = normalized;
            PersistResultGridFilterForTab(ActiveTab);
            RaiseSqlPanelPropertiesChanged();
        }
    }

    public string? ResultGridSortColumn => ActiveTab.ResultGridSortColumn;
    public bool ResultGridSortAscending => ActiveTab.ResultGridSortAscending;

    public void SetResultGridSort(string? column, bool ascending)
    {
        ActiveTab.ResultGridSortColumn = string.IsNullOrWhiteSpace(column) ? null : column;
        ActiveTab.ResultGridSortAscending = ascending;
        RaiseSqlPanelPropertiesChanged();
    }

    public bool HasExecutionHistory => ExecutionHistory.Count > 0;
    public bool IsExecutionHistoryEmpty => !HasExecutionHistory;
    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            string normalized = value ?? string.Empty;
            if (!Set(ref _historySearchText, normalized))
                return;

            if (_isHistoryClearConfirmationPending)
                _isHistoryClearConfirmationPending = false;

            EnsureHistorySelection();

            RaisePropertyChanged(nameof(FilteredExecutionHistory));
            RaisePropertyChanged(nameof(HasFilteredExecutionHistory));
            RaisePropertyChanged(nameof(IsFilteredExecutionHistoryEmpty));
            RaisePropertyChanged(nameof(HasHistorySearchNoResults));
            RaisePropertyChanged(nameof(HistoryFilterSummaryText));
            RaisePropertyChanged(nameof(SelectedExecutionHistoryEntry));
            RaisePropertyChanged(nameof(HasPendingHistoryClearConfirmation));
            RaisePropertyChanged(nameof(ClearHistoryButtonText));
        }
    }
    public IReadOnlyList<SqlEditorHistoryEntry> FilteredExecutionHistory
    {
        get
        {
            if (string.IsNullOrWhiteSpace(HistorySearchText))
                return ExecutionHistory;

            string needle = HistorySearchText.Trim();
            return ExecutionHistory
                .Where(entry => entry.Sql.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
    public bool HasFilteredExecutionHistory => FilteredExecutionHistory.Count > 0;
    public bool IsFilteredExecutionHistoryEmpty => !HasFilteredExecutionHistory;
    public SqlEditorHistoryEntry? SelectedExecutionHistoryEntry
    {
        get => ActiveTab.SelectedExecutionHistoryEntry;
        set
        {
            if (ReferenceEquals(ActiveTab.SelectedExecutionHistoryEntry, value))
                return;

            ActiveTab.SelectedExecutionHistoryEntry = value;
            RaisePropertyChanged(nameof(SelectedExecutionHistoryEntry));
        }
    }

    public void SelectNextHistoryEntry()
    {
        IReadOnlyList<SqlEditorHistoryEntry> items = FilteredExecutionHistory;
        if (items.Count == 0)
            return;

        int current = ResolveHistorySelectionIndex(items, SelectedExecutionHistoryEntry);
        int next = current < 0 ? 0 : Math.Min(current + 1, items.Count - 1);
        SelectedExecutionHistoryEntry = items[next];
    }

    public void SelectPreviousHistoryEntry()
    {
        IReadOnlyList<SqlEditorHistoryEntry> items = FilteredExecutionHistory;
        if (items.Count == 0)
            return;

        int current = ResolveHistorySelectionIndex(items, SelectedExecutionHistoryEntry);
        int previous = current <= 0 ? 0 : current - 1;
        SelectedExecutionHistoryEntry = items[previous];
    }

    public Task<SqlEditorResultSet?> ExecuteSelectedHistoryEntryAsync(int maxRows = 1000)
    {
        SqlEditorHistoryEntry? target = SelectedExecutionHistoryEntry ?? FilteredExecutionHistory.FirstOrDefault();
        return ExecuteHistoryEntryAsync(target, maxRows);
    }

    public bool HasHistorySearchNoResults =>
        HasExecutionHistory
        && !string.IsNullOrWhiteSpace(HistorySearchText)
        && IsFilteredExecutionHistoryEmpty;
    public string HistoryFilterSummaryText
    {
        get
        {
            int total = ExecutionHistory.Count;
            if (total == 0)
                return L("sqlEditor.history.count.none", "0 itens");

            int filtered = FilteredExecutionHistory.Count;
            if (string.IsNullOrWhiteSpace(HistorySearchText))
                return string.Format(
                    L("sqlEditor.history.count.total", "{0} itens"),
                    total);

            return string.Format(
                L("sqlEditor.history.count.filtered", "{0} de {1} itens"),
                filtered,
                total);
        }
    }
    public bool HasPendingHistoryClearConfirmation => _isHistoryClearConfirmationPending;
    public string ClearHistoryButtonText =>
        HasPendingHistoryClearConfirmation
            ? L("sqlEditor.history.clear.confirm", "Confirmar limpeza")
            : L("sqlEditor.history.clear", "Limpar historico");
    public string HistoryEmptyText => L("sqlEditor.history.empty", "Execute uma consulta para iniciar o historico.");
    public string MessagesEmptyText => L("sqlEditor.messages.empty", "As mensagens aparecem aqui apos a execucao.");
    public IReadOnlyList<SqlEditorSchemaTableItem> SchemaTables => BuildSchemaTables();
    public bool HasSchemaTables => SchemaTables.Count > 0;
    public bool IsSchemaEmpty => !HasSchemaTables;
    public string SchemaEmptyText => L("sqlEditor.schema.empty", "Sem metadados disponiveis. Conecte e recarregue para ver tabelas.");
    public IReadOnlyList<SqlEditorResultTab> ResultTabs => ActiveTab.ResultTabs;
    public int SelectedResultTabIndex
    {
        get => ActiveTab.SelectedResultTabIndex;
        set
        {
            int boundedValue = value;
            if (value >= ResultTabs.Count)
                boundedValue = ResultTabs.Count - 1;
            if (value < -1)
                boundedValue = -1;

            if (ActiveTab.SelectedResultTabIndex == boundedValue)
                return;

            ActiveTab.SelectedResultTabIndex = boundedValue;
            RaiseSqlPanelPropertiesChanged();
        }
    }
    public DataView? ResultRowsView => SelectedResultTab?.RowsView ?? ActiveTab.LastResult?.Data?.DefaultView;
    public bool HasResultRows => ResultRowsView is not null;
    public bool IsResultRowsEmpty => !HasResultRows;
    public string ResultsEmptyText => L("sqlEditor.results.empty", "Execute uma consulta para preencher os resultados.");
    public IReadOnlyList<SqlEditorHistoryEntry> ExecutionHistory => ActiveTab.ExecutionHistory;
    public SqlEditorExecutionTelemetry ExecutionTelemetry => ActiveTab.ExecutionTelemetry;
    public SqlEditorCompletionTelemetry CompletionTelemetry => ActiveTab.CompletionTelemetry;
    public string CompletionTelemetryText =>
        CompletionTelemetry.SampleCount == 0
            ? L("sqlEditor.completion.telemetry.none", "Completion: sem amostras ainda.")
            : string.Format(
                L(
                    "sqlEditor.completion.telemetry.summary",
                    "Completion p95: {0} ms    Ultima: {1} ms    Amostras: {2}    Budget<= {3} ms"),
                CompletionTelemetry.P95DurationMs,
                CompletionTelemetry.LastDurationMs,
                CompletionTelemetry.SampleCount,
                CompletionTelemetry.BudgetMs);
    public string ExecutionTelemetryText =>
        ExecutionTelemetry.StatementCount == 0
            ? L("sqlEditor.telemetry.none", "Sem telemetria de execucao ainda.")
            : string.Format(
                L("sqlEditor.telemetry.summary", "Instrucoes: {0}    Sucesso: {1}    Falhas: {2}    Total: {3} ms"),
                ExecutionTelemetry.StatementCount,
                ExecutionTelemetry.SuccessCount,
                ExecutionTelemetry.FailureCount,
                ExecutionTelemetry.TotalDurationMs);
    public string ExecutionTelemetryErrorsText =>
        ExecutionTelemetry.ErrorMessages.Count == 0
            ? L("sqlEditor.telemetry.errors.none", "Sem erros agregados.")
            : string.Join(Environment.NewLine, ExecutionTelemetry.ErrorMessages);
    public MutationGuardResult? PendingMutationGuard
    {
        get => _pendingMutationGuard;
        private set
        {
            if (!Set(ref _pendingMutationGuard, value))
                return;

            RaisePropertyChanged(nameof(HasPendingMutationConfirmation));
            RaisePropertyChanged(nameof(PendingMutationIssues));
            RaisePropertyChanged(nameof(PendingMutationCountQuery));
            RaisePropertyChanged(nameof(PendingMutationMessage));
            NotifyMutationCommands();
        }
    }

    public bool HasPendingMutationConfirmation => PendingMutationGuard is not null;
    public SqlMutationDiffPreview? PendingMutationDiff
    {
        get => _pendingMutationDiff;
        private set
        {
            if (!Set(ref _pendingMutationDiff, value))
                return;

            RaisePropertyChanged(nameof(HasPendingMutationDiff));
            RaisePropertyChanged(nameof(PendingMutationDiffText));
        }
    }
    public bool HasPendingMutationDiff => PendingMutationDiff is { Available: true };
    public string PendingMutationDiffText => PendingMutationDiff?.Message ?? L("sqlEditor.diff.none", "Sem diff transacional disponivel.");
    public long? PendingMutationEstimatedRows => _pendingMutationEstimatedRows;
    public string PendingMutationEstimateText =>
        !HasPendingMutationConfirmation
            ? L("sqlEditor.mutation.estimate.none", "Sem estimativa de mutacao disponivel.")
            : _pendingMutationEstimatedRows.HasValue
                ? string.Format(
                    L("sqlEditor.mutation.estimate.value", "Linhas afetadas estimadas: {0}"),
                    _pendingMutationEstimatedRows.Value)
                : L("sqlEditor.mutation.estimate.unavailable", "Nao foi possivel estimar as linhas afetadas automaticamente.");
    public bool HasPendingCloseTabConfirmation => _tabCloseWorkflowService.HasPendingConfirmation;
    public string PendingCloseTabMessage => _tabCloseWorkflowService.PendingMessage;
    public bool HasManyTabsWarning => EditorTabs.Count >= 15;
    public string ManyTabsWarningText => string.Format(
        L("sqlEditor.tab.manyWarning", "Quantidade alta de abas: {0} abas abertas."),
        EditorTabs.Count);
    public IReadOnlyList<MutationGuardIssue> PendingMutationIssues => PendingMutationGuard?.Issues ?? [];
    public string? PendingMutationCountQuery => PendingMutationGuard?.CountQuery;
    public string PendingMutationMessage =>
        PendingMutationGuard is null
            ? L("sqlEditor.mutation.pending.none", "Sem confirmacao de mutacao pendente.")
            : L("sqlEditor.mutation.pending.required", "A mutacao exige confirmacao antes da execucao.");
    public string LastExecutionMessage =>
        CurrentResult is null
            ? L("sqlEditor.message.empty", "Execute uma instrucao para ver mensagens.")
            : CurrentResult.ErrorMessage ?? L("sqlEditor.message.success", "Execucao concluida com sucesso.");
    public bool CanExportReport => CurrentResult is not null;
    public string ResultSummaryText
    {
        get
        {
            SqlEditorResultSet? result = CurrentResult;
            if (result is null)
                return L("sqlEditor.result.summary.empty", "Linhas: -    Tempo: -");

            string rows = result.RowsAffected?.ToString() ?? "-";
            long ms = (long)Math.Round(result.ExecutionTime.TotalMilliseconds);
            return string.Format(
                L("sqlEditor.result.summary", "Linhas: {0}    Tempo: {1} ms"),
                rows,
                ms);
        }
    }
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (!Set(ref _isExecuting, value))
                return;

            RaisePropertyChanged(nameof(CanExecuteOrCancel));
            RaisePropertyChanged(nameof(ExecuteOrCancelButtonText));
            RaisePropertyChanged(nameof(ExecuteOrCancelTooltipText));
        }
    }

    public bool IsCancellationPending
    {
        get => _isCancellationPending;
        private set
        {
            if (!Set(ref _isCancellationPending, value))
                return;

            RaisePropertyChanged(nameof(CanExecuteOrCancel));
            RaisePropertyChanged(nameof(ExecuteOrCancelButtonText));
            RaisePropertyChanged(nameof(ExecuteOrCancelTooltipText));
        }
    }

    public bool CanExecuteOrCancel => !IsExecuting || !IsCancellationPending;
    public string ExecuteOrCancelButtonText =>
        !IsExecuting
            ? L("sqlEditor.execute.run", "Executar")
            : IsCancellationPending
                ? L("sqlEditor.execute.stopping", "Parando...")
                : L("sqlEditor.execute.stop", "Parar");
    public string ExecuteOrCancelTooltipText =>
        !IsExecuting
            ? L("sqlEditor.execute.run.tooltip", "Executar selecao ou statement atual (F8)")
            : IsCancellationPending
                ? L("sqlEditor.execute.stopping.tooltip", "Parando execucao... (Esc)")
                : L("sqlEditor.execute.stop.tooltip", "Parar execucao (Esc)");
    public string CursorPositionTooltipText => L("sqlEditor.gotoLine.tooltip", "Ir para linha (Ctrl+G)");
    public int ActiveExecutionStatementStartLine
    {
        get => _activeExecutionStatementStartLine;
        private set
        {
            if (Set(ref _activeExecutionStatementStartLine, value))
                RaisePropertyChanged(nameof(HasActiveExecutionStatementRange));
        }
    }
    public int ActiveExecutionStatementEndLine
    {
        get => _activeExecutionStatementEndLine;
        private set
        {
            if (Set(ref _activeExecutionStatementEndLine, value))
                RaisePropertyChanged(nameof(HasActiveExecutionStatementRange));
        }
    }
    public bool HasActiveExecutionStatementRange =>
        ActiveExecutionStatementStartLine > 0
        && ActiveExecutionStatementEndLine >= ActiveExecutionStatementStartLine;

    public bool IsExplainRunning
    {
        get => _isExplainRunning;
        private set => Set(ref _isExplainRunning, value);
    }
    public string ExplainSummaryText
    {
        get => _explainSummaryText;
        private set => Set(ref _explainSummaryText, value ?? string.Empty);
    }
    public string ExplainRawOutput
    {
        get => _explainRawOutput;
        private set
        {
            if (!Set(ref _explainRawOutput, value ?? string.Empty))
                return;

            RaisePropertyChanged(nameof(HasExplainRawOutput));
        }
    }
    public bool HasExplainRawOutput => !string.IsNullOrWhiteSpace(ExplainRawOutput);

    public bool IsBenchmarkRunning
    {
        get => _isBenchmarkRunning;
        private set => Set(ref _isBenchmarkRunning, value);
    }
    public string BenchmarkProgressText
    {
        get => _benchmarkProgressText;
        private set => Set(ref _benchmarkProgressText, value ?? string.Empty);
    }
    public string BenchmarkSummaryText
    {
        get => _benchmarkSummaryText;
        private set
        {
            if (!Set(ref _benchmarkSummaryText, value ?? string.Empty))
                return;

            RaisePropertyChanged(nameof(HasBenchmarkResult));
        }
    }
    public bool HasBenchmarkResult => !string.IsNullOrWhiteSpace(BenchmarkSummaryText);

    public bool HasActiveConnection => ResolveConnectionConfigForActiveTab() is not null;
    public bool IsProductionConnectionContext
    {
        get
        {
            if (!HasActiveConnection)
                return false;

            string source = (ActiveTabConnectionProfile?.DisplayName ?? ActiveConnectionDisplayName).ToLowerInvariant();
            return source.Contains("prod", StringComparison.Ordinal)
                || source.Contains("production", StringComparison.Ordinal);
        }
    }

    public bool IsStagingConnectionContext
    {
        get
        {
            if (!HasActiveConnection || IsProductionConnectionContext)
                return false;

            string source = (ActiveTabConnectionProfile?.DisplayName ?? ActiveConnectionDisplayName).ToLowerInvariant();
            return source.Contains("stag", StringComparison.Ordinal)
                || source.Contains("staging", StringComparison.Ordinal);
        }
    }

    public bool IsNeutralConnectionContext =>
        HasActiveConnection
        && !IsProductionConnectionContext
        && !IsStagingConnectionContext;

    public bool HasNoActiveConnection => !HasActiveConnection;

    public string ActiveConnectionContextBadgeText
    {
        get
        {
            ConnectionConfig? config = ResolveConnectionConfigForActiveTab();
            if (config is null)
                return L("sqlEditor.connection.none", "Sem conexao ativa.");

            string provider = GetProviderDisplayName(config.Provider);
            string profile = ActiveTabConnectionProfile?.DisplayName ?? config.Database;
            string schema = SharedConnectionManager?.SelectedSchema ?? "default";
            return $"[{provider}] {profile}/{schema}";
        }
    }

    public int CursorLine
    {
        get => _cursorLine;
        private set
        {
            if (value < 1)
                value = 1;

            Set(ref _cursorLine, value);
        }
    }

    public int CursorColumn
    {
        get => _cursorColumn;
        private set
        {
            if (value < 1)
                value = 1;

            Set(ref _cursorColumn, value);
        }
    }

    public string CursorPositionText => $"Ln {CursorLine}, Col {CursorColumn}";
    public string IndentationStatusText => L("sqlEditor.status.indentation", "Espacos: 2");
    public string ActiveProviderStatusText
    {
        get
        {
            ConnectionConfig? config = ResolveConnectionConfigForActiveTab();
            if (config is null)
                return GetProviderDisplayName(ActiveTab.Provider);

            string providerName = GetProviderDisplayName(config.Provider);
            string? serverVersion = _metadataResolver()?.ServerVersion;
            if (string.IsNullOrWhiteSpace(serverVersion))
                return providerName;

            string normalizedVersion = serverVersion.Trim();
            return $"{providerName} {normalizedVersion}";
        }
    }

    public bool HasExecutionError
    {
        get => _hasExecutionError;
        private set => Set(ref _hasExecutionError, value);
    }

    public string ExecutionStatusText
    {
        get => _executionStatusText;
        private set => Set(ref _executionStatusText, value);
    }

    public string? ExecutionDetailText
    {
        get => _executionDetailText;
        private set => Set(ref _executionDetailText, value);
    }

    public void ReceiveFromCanvas(string sql, DatabaseProvider provider)
    {
        Tabs.ReceiveFromCanvas(sql, provider);
        PersistSessionDraftsSnapshot();
        RaiseTabStateChanged();
        RaiseSqlPanelPropertiesChanged();
    }

    public string? GetSqlForExecution(int selectionStart, int selectionLength, int caretOffset)
    {
        return _selectionExtractor.ExtractSelectionOrCurrentStatement(
            ActiveTab.SqlText,
            selectionStart,
            selectionLength,
            caretOffset);
    }

    public SqlCompletionRequest GetCompletionRequest(string fullText, int caretOffset)
    {
        DbMetadata? metadata = _metadataResolver();
        return _completionProvider.GetSuggestions(
            fullText,
            caretOffset,
            metadata,
            ActiveTabProvider,
            ActiveTabConnectionProfileId);
    }

    public void RecordCompletionSuggestionAccepted(string? suggestionLabel)
    {
        if (string.IsNullOrWhiteSpace(suggestionLabel))
            return;

        _completionProvider.RecordAcceptedSuggestion(suggestionLabel, ActiveTabConnectionProfileId);
    }

    public void RecordCompletionLatency(TimeSpan latency)
    {
        long durationMs = Math.Max(0, (long)Math.Round(latency.TotalMilliseconds));
        ActiveTab.CompletionTelemetry = ActiveTab.CompletionTelemetryTracker.AddSample(durationMs);
        RaiseSqlPanelPropertiesChanged();
    }

    public async Task<bool> SaveActiveTabAsync(string? filePath = null, CancellationToken ct = default)
    {
        string? resolvedPath = filePath ?? ActiveTab.FilePath;
        SqlEditorFileSaveOutcome outcome = await _fileService.SaveAsync(ActiveTab.SqlText, resolvedPath, ct);
        ExecutionStatusText = outcome.StatusText;
        ExecutionDetailText = outcome.DetailText;
        HasExecutionError = outcome.HasError;

        if (!outcome.Success || string.IsNullOrWhiteSpace(resolvedPath))
            return false;

        ActiveTab.FilePath = resolvedPath;
        ActiveTab.FallbackTitle = Path.GetFileName(resolvedPath);
        ActiveTab.IsDirty = false;
        PersistSessionDraftsSnapshot();
        RaiseTabStateChanged();
        return true;
    }

    public async Task<bool> OpenSqlFileAsync(string filePath, CancellationToken ct = default)
    {
        SqlEditorFileOpenOutcome outcome = await _fileService.OpenAsync(filePath, ct);
        ExecutionStatusText = outcome.StatusText;
        ExecutionDetailText = outcome.DetailText;
        HasExecutionError = outcome.HasError;

        if (!outcome.Success || outcome.Content is null)
            return false;

        SqlEditorTabState active = ActiveTab;
        SqlEditorTabState target = string.IsNullOrWhiteSpace(active.SqlText) && !active.IsDirty
            ? active
            : Tabs.AddNewTab(active.Provider, active.ConnectionProfileId);

        target.SqlText = outcome.Content;
        target.FilePath = filePath;
        target.FallbackTitle = Path.GetFileName(filePath);
        target.IsDirty = false;
        PersistSessionDraftsSnapshot();
        RaiseTabStateChanged();
        return true;
    }

    public async Task<SqlEditorResultSet> ExecuteSelectionOrCurrentAsync(
        int selectionStart,
        int selectionLength,
        int caretOffset,
        int maxRows = 1000)
    {
        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        IsExecuting = true;
        IsCancellationPending = false;
        NotifyCommands();
        HasExecutionError = false;
        ExecutionStatusText = L("sqlEditor.status.executing", "Executando SQL...");
        ExecutionDetailText = null;
        BeginExecutionProgress(totalStatements: 1);

        try
        {
            string? sql = GetSqlForExecution(selectionStart, selectionLength, caretOffset);
            UpdateActiveExecutionStatementRange(ResolveStatementLineRangeForCaret(caretOffset));
            SqlEditorResultSet result = await ExecuteSqlAsync(sql, maxRows, enforceMutationGuard: true);

            if (!result.Success && string.Equals(result.ErrorMessage, MutationConfirmationRequiredError(), StringComparison.Ordinal))
                return result;

            StoreResult(result);
            OpenResultsSheet();
            UpdateExecutionTelemetry([result]);
            UpdateExecutionFeedback(result);
            RaiseSqlPanelPropertiesChanged();
            return result;
        }
        finally
        {
            EndExecutionProgress();
            IsExecuting = false;
            NotifyCommands();
        }
    }

    public void UpdateCursorPosition(int line, int column)
    {
        bool changed = false;
        if (line > 0 && line != CursorLine)
        {
            CursorLine = line;
            changed = true;
        }

        if (column > 0 && column != CursorColumn)
        {
            CursorColumn = column;
            changed = true;
        }

        if (changed)
            RaisePropertyChanged(nameof(CursorPositionText));
    }

    public async Task<IReadOnlyList<SqlEditorResultSet>> ExecuteAllAsync(int maxRows = 1000)
    {
        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        IsExecuting = true;
        IsCancellationPending = false;
        NotifyCommands();
        HasExecutionError = false;
        ExecutionStatusText = L("sqlEditor.status.executingScript", "Executando script SQL...");
        ExecutionDetailText = null;

        var results = new List<SqlEditorResultSet>();
        try
        {
            IReadOnlyList<SqlStatement> statements = _statementSplitter.Split(ActiveTab.SqlText);
            BeginExecutionProgress(totalStatements: Math.Max(1, statements.Count));
            if (statements.Count == 0)
            {
                SqlEditorResultSet empty = await ExecuteSqlAsync(null, maxRows, enforceMutationGuard: true);
                StoreResult(empty);
                OpenResultsSheet();
                UpdateExecutionTelemetry([empty]);
                UpdateExecutionFeedback(empty);
                RaiseSqlPanelPropertiesChanged();
                results.Add(empty);
                return results;
            }

            for (int i = 0; i < statements.Count; i++)
            {
                SqlStatement statement = statements[i];
                UpdateActiveExecutionStatementRange(statement.StartLine, statement.EndLine);
                UpdateExecutionProgressStatement(i + 1, statements.Count);
                ExecutionStatusText = string.Format(
                    L("sqlEditor.status.executingStep", "Executando {0}/{1}..."),
                    i + 1,
                    statements.Count);
                SqlEditorResultSet result = await ExecuteSqlAsync(statement.Sql, maxRows, enforceMutationGuard: true);
                results.Add(result);

                if (!result.Success && string.Equals(result.ErrorMessage, MutationConfirmationRequiredError(), StringComparison.Ordinal))
                    break;

                StoreResult(result);
                OpenResultsSheet();
                UpdateExecutionFeedback(result);
                RaiseSqlPanelPropertiesChanged();

                if (!result.Success)
                    break;
            }

            UpdateExecutionTelemetry(results);

            return results;
        }
        finally
        {
            EndExecutionProgress();
            IsExecuting = false;
            NotifyCommands();
        }
    }

    public void CancelExecution()
    {
        _executionCts?.Cancel();
        if (IsExecuting)
        {
            IsCancellationPending = true;
            ExecutionStatusText = L("sqlEditor.status.canceling", "Cancelando execucao...");
        }
    }

    public async Task RunExplainAsync(bool includeAnalyze = false)
    {
        string sql = ActiveTab.SqlText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sql))
        {
            ExplainSummaryText = L("sqlEditor.explain.empty", "Nada para explicar. Escreva um SQL primeiro.");
            ExplainRawOutput = string.Empty;
            return;
        }

        _explainCts?.Cancel();
        _explainCts?.Dispose();
        _explainCts = new CancellationTokenSource();
        IsExplainRunning = true;
        ExplainSummaryText = L("sqlEditor.explain.running", "Executando explain...");

        try
        {
            ExplainResult result = await _explainExecutor.RunAsync(
                sql,
                ActiveTabProvider,
                ResolveConnectionConfigForActiveTab(),
                new ExplainOptions(IncludeAnalyze: includeAnalyze, IncludeBuffers: false, Format: ExplainFormat.Text),
                _explainCts.Token);

            string planning = result.PlanningTimeMs.HasValue
                ? $"{result.PlanningTimeMs.Value:0.###} ms"
                : "-";
            string execution = result.ExecutionTimeMs.HasValue
                ? $"{result.ExecutionTimeMs.Value:0.###} ms"
                : "-";
            ExplainSummaryText = string.Format(
                L("sqlEditor.explain.summary", "Explain: {0} etapas  Planning: {1}  Execution: {2}"),
                result.Nodes.Count,
                planning,
                execution);
            ExplainRawOutput = result.RawOutput;
        }
        catch (OperationCanceledException)
        {
            ExplainSummaryText = L("sqlEditor.explain.canceled", "Explain cancelado.");
        }
        catch (Exception ex)
        {
            ExplainSummaryText = string.Format(
                L("sqlEditor.explain.failed", "Falha ao executar explain: {0}"),
                ex.Message);
            ExplainRawOutput = string.Empty;
        }
        finally
        {
            IsExplainRunning = false;
        }
    }

    public async Task RunBenchmarkAsync(int iterations = 8, int warmupIterations = 2, int intervalMs = 0)
    {
        string sql = ActiveTab.SqlText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sql))
        {
            BenchmarkSummaryText = L("sqlEditor.benchmark.empty", "Nada para medir. Escreva um SQL primeiro.");
            return;
        }

        _benchmarkCts?.Cancel();
        _benchmarkCts?.Dispose();
        _benchmarkCts = new CancellationTokenSource();
        IsBenchmarkRunning = true;
        BenchmarkProgressText = L("sqlEditor.benchmark.running", "Executando benchmark...");
        BenchmarkSummaryText = string.Empty;

        var iterationExecutor = new AdaptiveBenchmarkIterationExecutor(
            connectionResolver: ResolveConnectionConfigForActiveTab,
            sqlResolver: () => ActiveTab.SqlText ?? string.Empty);
        var runner = new BenchmarkRunner(iterationExecutor);
        var executionService = new BenchmarkExecutionService(runner);

        try
        {
            BenchmarkRunConfiguration config = new(iterations, warmupIterations, intervalMs);
            BenchmarkRunResult result = await executionService.ExecuteAsync(
                runLabel: $"SQL Editor {DateTime.Now:HH:mm:ss}",
                configuration: config,
                onProgress: progress =>
                {
                    BenchmarkProgressText = $"{progress.Stage} {progress.Completed}/{progress.Total}";
                },
                cancellationToken: _benchmarkCts.Token);

            _latestBenchmarkResult = result;
            BenchmarkSummaryText = result.Summary;
            BenchmarkProgressText = L("sqlEditor.benchmark.finished", "Benchmark concluido.");
        }
        catch (OperationCanceledException)
        {
            BenchmarkProgressText = L("sqlEditor.benchmark.canceled", "Benchmark cancelado.");
        }
        catch (Exception ex)
        {
            BenchmarkSummaryText = string.Format(
                L("sqlEditor.benchmark.failed", "Falha no benchmark: {0}"),
                ex.Message);
        }
        finally
        {
            IsBenchmarkRunning = false;
        }
    }

    public void CancelBenchmark()
    {
        _benchmarkCts?.Cancel();
    }

    public async Task<SqlEditorResultSet?> ExecuteHistoryEntryAsync(SqlEditorHistoryEntry? entry, int maxRows = 1000)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Sql))
            return null;

        ActiveTab.SqlText = entry.Sql;
        ActiveTab.IsDirty = true;
        NotifyActiveTabEdited();
        RaiseTabStateChanged();

        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        IsExecuting = true;
        IsCancellationPending = false;
        NotifyCommands();
        HasExecutionError = false;
        ExecutionStatusText = L("sqlEditor.status.executingHistory", "Executando instrucao do historico...");
        ExecutionDetailText = null;
        BeginExecutionProgress(totalStatements: 1);

        try
        {
            ResetActiveExecutionStatementRange();
            SqlEditorResultSet result = await ExecuteSqlAsync(entry.Sql, maxRows, enforceMutationGuard: true);

            if (!result.Success && string.Equals(result.ErrorMessage, MutationConfirmationRequiredError(), StringComparison.Ordinal))
                return result;

            StoreResult(result);
            OpenResultsSheet();
            UpdateExecutionTelemetry([result]);
            UpdateExecutionFeedback(result);
            RaiseSqlPanelPropertiesChanged();
            return result;
        }
        finally
        {
            EndExecutionProgress();
            IsExecuting = false;
            NotifyCommands();
        }
    }

    public void UseHistoryEntryInEditor(SqlEditorHistoryEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Sql))
            return;

        ActiveTab.SqlText = entry.Sql;
        ActiveTab.IsDirty = true;
        NotifyActiveTabEdited();
        ExecutionStatusText = L("sqlEditor.status.historyLoaded", "Instrucao do historico carregada no editor.");
        ExecutionDetailText = null;
        HasExecutionError = false;
        RaiseTabStateChanged();
    }

    public bool RequestClearExecutionHistory()
    {
        if (!HasExecutionHistory)
            return false;

        if (!HasPendingHistoryClearConfirmation)
        {
            _isHistoryClearConfirmationPending = true;
            RaisePropertyChanged(nameof(HasPendingHistoryClearConfirmation));
            RaisePropertyChanged(nameof(ClearHistoryButtonText));
            PublishStatus(
                L("sqlEditor.status.historyClearConfirm", "Clique em limpar novamente para confirmar a remocao do historico."),
                null,
                false);
            return false;
        }

        ActiveTab.ExecutionHistory = [];
        ActiveTab.IsExecutionHistoryHydratedFromSettings = true;
        ActiveTab.SelectedExecutionHistoryEntry = null;
        _isHistoryClearConfirmationPending = false;
        HistorySearchText = string.Empty;
        ClearExecutionHistoryForActiveProfileAsync();
        PublishStatus(
            L("sqlEditor.status.historyCleared", "Historico de execucao limpo."),
            null,
            false);
        RaiseSqlPanelPropertiesChanged();
        return true;
    }

    public async Task<SqlEditorResultSet?> ConfirmPendingMutationAsync(int maxRows = 1000)
    {
        if (!HasPendingMutationConfirmation || string.IsNullOrWhiteSpace(_pendingMutationSql))
            return null;

        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        IsExecuting = true;
        IsCancellationPending = false;
        NotifyCommands();
        HasExecutionError = false;
        ExecutionStatusText = L("sqlEditor.status.executingConfirmedMutation", "Executando mutacao confirmada...");
        ExecutionDetailText = null;
        BeginExecutionProgress(totalStatements: 1);

        try
        {
            string sql = _pendingMutationSql;
            ClearPendingMutation();
            SqlEditorResultSet result = await ExecuteSqlAsync(sql, maxRows, enforceMutationGuard: false);

            StoreResult(result);
            OpenResultsSheet();
            UpdateExecutionTelemetry([result]);
            UpdateExecutionFeedback(result);
            RaiseSqlPanelPropertiesChanged();
            return result;
        }
        finally
        {
            EndExecutionProgress();
            IsExecuting = false;
            NotifyCommands();
        }
    }

    public void CancelPendingMutation()
    {
        if (!HasPendingMutationConfirmation)
            return;

        ClearPendingMutation();
        ExecutionStatusText = L("sqlEditor.status.mutationCanceled", "Execucao da mutacao cancelada.");
        ExecutionDetailText = L("sqlEditor.detail.statementNotExecuted", "A instrucao nao foi executada.");
        HasExecutionError = false;
    }

    private void UpdateExecutionFeedback(SqlEditorResultSet result)
    {
        SqlEditorExecutionFeedback feedback = _executionFeedbackService.Build(result);
        ExecutionStatusText = feedback.StatusText;
        ExecutionDetailText = feedback.DetailText;
        HasExecutionError = feedback.HasError;
    }

    private async Task<SqlEditorResultSet> ExecuteSqlAsync(string? sql, int maxRows, bool enforceMutationGuard)
    {
        ConnectionConfig? config = ResolveConnectionConfigForActiveTab();
        SqlEditorMutationExecutionOutcome outcome = await _mutationExecutionOrchestrator.ExecuteAsync(
            sql,
            config,
            maxRows,
            enforceMutationGuard,
            BuildEstimateCacheKey(sql),
            _executionCts?.Token ?? CancellationToken.None);

        if (!outcome.RequiresConfirmation || outcome.ConfirmationState is null)
            return outcome.Result;

        PendingMutationGuard = outcome.ConfirmationState.Guard;
        _pendingMutationSql = outcome.ConfirmationState.StatementSql;
        _pendingMutationEstimatedRows = outcome.ConfirmationState.EstimatedRows;
        PendingMutationDiff = outcome.ConfirmationState.DiffPreview;
        ExecutionStatusText = L("sqlEditor.status.confirmationRequired", "Confirmacao necessaria antes da execucao.");
        ExecutionDetailText = outcome.ConfirmationState.Guard.Issues.FirstOrDefault()?.Message;
        HasExecutionError = false;
        RaiseSqlPanelPropertiesChanged();
        return outcome.Result;
    }

    private void ClearPendingMutation()
    {
        PendingMutationGuard = null;
        PendingMutationDiff = null;
        _pendingMutationSql = null;
        _pendingMutationEstimatedRows = null;
    }

    private void NotifyMutationCommands()
    {
        _commandNotifier.NotifyMutationCommands(
            ConfirmPendingMutationCommand,
            CancelPendingMutationCommand);
    }

    private void NotifyTabCommands()
    {
        _commandNotifier.NotifyTabCommands(
            NewTabCommand,
            CloseTabCommand,
            CloseActiveTabCommand,
            ConfirmPendingCloseTabCommand,
            CancelPendingCloseTabCommand);
    }

    private void NotifyCommands()
    {
        _commandNotifier.NotifyAll(
            ConfirmPendingMutationCommand,
            CancelPendingMutationCommand,
            NewTabCommand,
            CloseTabCommand,
            CloseActiveTabCommand,
            ConfirmPendingCloseTabCommand,
            CancelPendingCloseTabCommand);

        (CloseResultsSheetCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (OpenConnectionSwitcherCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ExecuteHistoryEntryCommand as RelayCommand<SqlEditorHistoryEntry>)?.NotifyCanExecuteChanged();
    }

    private void RaiseSqlPanelPropertiesChanged()
    {
        SyncDialectFromConnection();
        _propertyChangePublisher.PublishSqlPanelChanges(RaisePropertyChanged);
    }

    public void NotifyConnectionContextChanged()
    {
        RaisePropertyChanged(nameof(SharedConnectionManager));
        RaisePropertyChanged(nameof(HasSharedConnectionManager));
        RaiseSqlPanelPropertiesChanged();
    }

    public bool TryBuildReportExportContext(out SqlEditorReportExportContext? context)
    {
        SqlEditorResultSet? result = CurrentResult;
        if (result is null)
        {
            context = null;
            return false;
        }

        var columns = new List<string>();
        var resultRows = new List<IReadOnlyDictionary<string, object?>>();
        if (result.Data is not null)
        {
            foreach (DataColumn column in result.Data.Columns)
                columns.Add(column.ColumnName);

            foreach (DataRow row in result.Data.Rows)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn column in result.Data.Columns)
                    values[column.ColumnName] = NormalizeReportCellValue(row[column]);

                resultRows.Add(values);
            }
        }

        string status = result.Success ? "success" : "error";
        if (result.Success && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            status = "warning";

        long? executionMs = (long)Math.Round(result.ExecutionTime.TotalMilliseconds);
        long? rowCount = result.RowsAffected;
        if (!rowCount.HasValue && result.Data is not null)
            rowCount = result.Data.Rows.Count;

        context = new SqlEditorReportExportContext(
            Sql: result.StatementSql,
            SchemaColumns: columns,
            ResultRows: resultRows,
            ExecutionResult: new SqlEditorReportExecutionResult(
                RowCount: rowCount,
                ExecutionTimeMs: executionMs,
                Status: status,
                ErrorMessage: result.ErrorMessage),
            Connection: ResolveConnectionConfigForActiveTab(),
            ActiveFilePath: ActiveTab.FilePath,
            TabTitle: ActiveTab.FallbackTitle);

        return true;
    }

    private static object? NormalizeReportCellValue(object? value)
    {
        if (value is null || value is DBNull)
            return null;

        return value switch
        {
            DateTimeOffset dto => dto.ToString("O"),
            DateTime dt => dt.ToString("O"),
            TimeSpan ts => ts.ToString(),
            Guid guid => guid.ToString("D"),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => value,
        };
    }

    public void PublishStatus(string statusText, string? detailText = null, bool hasError = false)
    {
        ExecutionStatusText = statusText;
        ExecutionDetailText = detailText;
        HasExecutionError = hasError;
    }

    private void StoreResult(SqlEditorResultSet result)
    {
        _isHistoryClearConfirmationPending = false;
        _resultStateService.AppendResult(ActiveTab, result);
        PersistExecutionHistoryForActiveProfileAsync();
        SelectedExecutionHistoryEntry = ActiveTab.ExecutionHistory.FirstOrDefault();
        RaisePropertyChanged(nameof(CanExportReport));
        RaisePropertyChanged(nameof(ShouldShowResultsSheet));
        RaisePropertyChanged(nameof(FilteredExecutionHistory));
        RaisePropertyChanged(nameof(HasFilteredExecutionHistory));
        RaisePropertyChanged(nameof(IsFilteredExecutionHistoryEmpty));
        RaisePropertyChanged(nameof(HasHistorySearchNoResults));
        RaisePropertyChanged(nameof(HistoryFilterSummaryText));
        RaisePropertyChanged(nameof(HasHiddenColumnUndo));
        RaisePropertyChanged(nameof(UndoHiddenColumnButtonText));
        RaisePropertyChanged(nameof(SelectedExecutionHistoryEntry));
        RaisePropertyChanged(nameof(HasPendingHistoryClearConfirmation));
        RaisePropertyChanged(nameof(ClearHistoryButtonText));
    }

    private void UpdateExecutionTelemetry(IReadOnlyList<SqlEditorResultSet> results)
    {
        if (results.Count == 0)
            return;

        ActiveTab.ExecutionTelemetry = _resultStateService.BuildTelemetry(results);
    }

    private void AddNewTab()
    {
        SqlEditorTabState tab = Tabs.AddNewTab();
        TryHydrateResultFilterForTab(tab, force: true);
        TryHydrateExecutionHistoryForTab(tab, force: true);
        PersistSessionDraftsSnapshot();
        RaiseTabStateChanged();
    }

    private void CloseTabById(string? tabId)
    {
        SqlEditorTabCloseOutcome outcome = _tabCloseWorkflowService.RequestClose(Tabs, tabId);
        if (!string.IsNullOrWhiteSpace(outcome.StatusText))
        {
            ExecutionStatusText = outcome.StatusText;
            ExecutionDetailText = outcome.DetailText;
            HasExecutionError = outcome.HasError;
        }

        PersistSessionDraftsSnapshot();
        RaiseTabStateChanged();
    }

    private bool CanCloseTab(string? tabId)
    {
        return _tabCloseWorkflowService.CanCloseTab(Tabs, tabId);
    }

    private void ConfirmPendingCloseTab()
    {
        SqlEditorTabCloseOutcome outcome = _tabCloseWorkflowService.ConfirmClose(Tabs);
        if (!string.IsNullOrWhiteSpace(outcome.StatusText))
        {
            ExecutionStatusText = outcome.StatusText;
            ExecutionDetailText = outcome.DetailText;
            HasExecutionError = outcome.HasError;
        }

        PersistSessionDraftsSnapshot();
        RaiseTabStateChanged();
    }

    private void CancelPendingCloseTab()
    {
        SqlEditorTabCloseOutcome outcome = _tabCloseWorkflowService.CancelClose();
        if (!string.IsNullOrWhiteSpace(outcome.StatusText))
        {
            ExecutionStatusText = outcome.StatusText;
            ExecutionDetailText = outcome.DetailText;
            HasExecutionError = outcome.HasError;
            RaiseTabStateChanged();
        }
    }

    private void RaiseTabStateChanged()
    {
        _isHistoryClearConfirmationPending = false;
        TryHydrateResultFilterForTab(ActiveTab);
        TryHydrateExecutionHistoryForTab(ActiveTab);
        EnsureHistorySelection();
        SyncTabCommands();
        SyncDialectFromConnection();
        _propertyChangePublisher.PublishTabStateChanges(RaisePropertyChanged);
        RaiseSqlPanelPropertiesChanged();
        RaisePropertyChanged(nameof(CanExportReport));
        RaisePropertyChanged(nameof(ShouldShowResultsSheet));
        RaisePropertyChanged(nameof(HasHiddenColumnUndo));
        RaisePropertyChanged(nameof(UndoHiddenColumnButtonText));
        RaisePropertyChanged(nameof(SelectedExecutionHistoryEntry));
        RaisePropertyChanged(nameof(HasPendingHistoryClearConfirmation));
        RaisePropertyChanged(nameof(ClearHistoryButtonText));
        RaisePropertyChanged(nameof(HistoryFilterSummaryText));
        RaisePropertyChanged(nameof(ActiveConnectionContextBadgeText));
        RaisePropertyChanged(nameof(HasActiveConnection));
        RaisePropertyChanged(nameof(IsProductionConnectionContext));
        RaisePropertyChanged(nameof(IsStagingConnectionContext));
        RaisePropertyChanged(nameof(ActiveProviderStatusText));
        RaisePropertyChanged(nameof(ExecuteOrCancelTooltipText));
        RaisePropertyChanged(nameof(CursorPositionTooltipText));
        NotifyCommands();
    }

    public void NotifyActiveTabEdited()
    {
        _hasPendingDraftAutoSave = true;
        EnsureDraftAutoSaveTimersStarted();
        _draftAutoSaveDebounceTimer?.Change(DraftAutoSaveDebounce, Timeout.InfiniteTimeSpan);
    }

    private void SyncTabCommands()
    {
        foreach (SqlEditorTabState tab in Tabs.Tabs)
            tab.CloseCommand = CloseTabCommand;
    }

    private SqlEditorResultTab? SelectedResultTab
    {
        get
        {
            int selectedIndex = ActiveTab.SelectedResultTabIndex;
            if (selectedIndex < 0 || selectedIndex >= ActiveTab.ResultTabs.Count)
                return null;

            return ActiveTab.ResultTabs[selectedIndex];
        }
    }

    private SqlEditorResultSet? CurrentResult => SelectedResultTab?.Result ?? ActiveTab.LastResult;

    private ConnectionConfig? ResolveConnectionConfigForActiveTab()
    {
        ConnectionConfig? profileConfig = _connectionConfigByProfileIdResolver(ActiveTab.ConnectionProfileId);
        return profileConfig ?? _connectionConfigResolver();
    }

    private void OpenConnectionSwitcher()
    {
        IReadOnlyList<SqlEditorConnectionProfileOption> profiles = AvailableConnectionProfiles;
        if (profiles.Count == 0)
            return;

        int currentIndex = profiles
            .Select((profile, index) => new { profile, index })
            .FirstOrDefault(x => string.Equals(x.profile.Id, ActiveTabConnectionProfileId, StringComparison.Ordinal))
            ?.index ?? -1;

        int nextIndex = (currentIndex + 1) % profiles.Count;
        SqlEditorConnectionProfileOption next = profiles[nextIndex];
        if (string.IsNullOrWhiteSpace(next.Id))
            return;

        ActiveTabConnectionProfileId = next.Id;
    }

    private void OpenResultsSheet()
    {
        IsResultsSheetOpen = true;
    }

    private void CloseResultsSheet()
    {
        IsResultsSheetOpen = false;
    }

    private void SyncDialectFromConnection()
    {
        ConnectionConfig? config = ResolveConnectionConfigForActiveTab();
        if (config is null)
            return;

        if (ActiveTab.Provider != config.Provider)
            ActiveTab.Provider = config.Provider;
    }

    private void BeginExecutionProgress(int totalStatements)
    {
        _executionStatusTimer?.Dispose();
        _executionStatusTimer = null;
        _executionStopwatch = Stopwatch.StartNew();
        _activeExecutionTotalStatements = Math.Max(1, totalStatements);
        _activeExecutionStatementIndex = 1;
        UpdateExecutingStatusText();
        _executionStatusTimer = new Timer(
            static state => ((SqlEditorViewModel)state!).OnExecutionStatusTimerTick(),
            this,
            dueTime: TimeSpan.FromMilliseconds(100),
            period: TimeSpan.FromMilliseconds(100));
    }

    private void UpdateExecutionProgressStatement(int currentStatementIndex, int totalStatements)
    {
        _activeExecutionTotalStatements = Math.Max(1, totalStatements);
        _activeExecutionStatementIndex = Math.Clamp(currentStatementIndex, 1, _activeExecutionTotalStatements);
        UpdateExecutingStatusText();
    }

    private void EndExecutionProgress()
    {
        _executionStatusTimer?.Dispose();
        _executionStatusTimer = null;
        _executionStopwatch?.Stop();
        _executionStopwatch = null;
        _activeExecutionTotalStatements = 0;
        _activeExecutionStatementIndex = 0;
        ResetActiveExecutionStatementRange();
        IsCancellationPending = false;
    }

    private void OnExecutionStatusTimerTick()
    {
        if (!IsExecuting)
            return;

        UpdateExecutingStatusText();
    }

    private void UpdateExecutingStatusText()
    {
        if (!IsExecuting)
            return;

        if (IsCancellationPending)
        {
            ExecutionStatusText = L("sqlEditor.status.canceling", "Cancelando execucao...");
            return;
        }

        double elapsedSeconds = Math.Max(0, _executionStopwatch?.Elapsed.TotalSeconds ?? 0);
        string elapsedText = string.Format(CultureInfo.InvariantCulture, "{0:0.0}s", elapsedSeconds);

        if (_activeExecutionTotalStatements > 1)
        {
            ExecutionStatusText = string.Format(
                L("sqlEditor.status.executingStatementTimer", "Executando statement {0} de {1} — {2}"),
                Math.Clamp(_activeExecutionStatementIndex, 1, _activeExecutionTotalStatements),
                _activeExecutionTotalStatements,
                elapsedText);
            return;
        }

        ExecutionStatusText = string.Format(
            L("sqlEditor.status.executingTimer", "Executando... {0}"),
            elapsedText);
    }

    private (int StartLine, int EndLine) ResolveStatementLineRangeForCaret(int caretOffset)
    {
        string sql = ActiveTab.SqlText ?? string.Empty;
        IReadOnlyList<SqlStatement> statements = _statementSplitter.Split(sql);
        if (statements.Count == 0)
            return (0, 0);

        int caretLine = ResolveLineFromOffset(sql, caretOffset);
        SqlStatement target = statements.FirstOrDefault(statement =>
            caretLine >= statement.StartLine && caretLine <= statement.EndLine)
            ?? statements[0];

        return (target.StartLine, target.EndLine);
    }

    private static int ResolveLineFromOffset(string text, int offset)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        int boundedOffset = Math.Clamp(offset, 0, text.Length);
        int line = 1;
        for (int i = 0; i < boundedOffset; i++)
        {
            if (text[i] == '\n')
                line++;
        }

        return line;
    }

    private void UpdateActiveExecutionStatementRange((int StartLine, int EndLine) range)
    {
        UpdateActiveExecutionStatementRange(range.StartLine, range.EndLine);
    }

    private void UpdateActiveExecutionStatementRange(int startLine, int endLine)
    {
        if (startLine <= 0 || endLine <= 0 || endLine < startLine)
        {
            ResetActiveExecutionStatementRange();
            return;
        }

        ActiveExecutionStatementStartLine = startLine;
        ActiveExecutionStatementEndLine = endLine;
    }

    private void ResetActiveExecutionStatementRange()
    {
        ActiveExecutionStatementStartLine = 0;
        ActiveExecutionStatementEndLine = 0;
    }

    private static string GetProviderDisplayName(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.Postgres => "PostgreSQL",
            DatabaseProvider.MySql => "MySQL",
            DatabaseProvider.SqlServer => "SQL Server",
            DatabaseProvider.SQLite => "SQLite",
            _ => provider.ToString(),
        };
    }

    private IReadOnlyList<SqlEditorSchemaTableItem> BuildSchemaTables()
    {
        DbMetadata? metadata = _metadataResolver();
        if (metadata is null)
            return [];

        return metadata.Schemas
            .SelectMany(schema => schema.Tables.Select(table => new SqlEditorSchemaTableItem
            {
                Schema = schema.Name,
                Name = table.Name,
                FullName = table.FullName,
                Columns = table.Columns
                    .OrderBy(column => column.OrdinalPosition)
                    .Select(column => new SqlEditorSchemaColumnItem
                    {
                        Name = column.Name,
                        DataType = column.DataType,
                        IsPrimaryKey = column.IsPrimaryKey,
                    })
                    .ToList(),
            }))
            .OrderBy(table => table.Schema)
            .ThenBy(table => table.Name)
            .ToList();
    }

    private string MutationConfirmationRequiredError() =>
        L("sqlEditor.error.mutationConfirmationRequired", "Confirmacao de mutacao necessaria.");

    private string BuildEstimateCacheKey(string? sql)
    {
        string statement = sql?.Trim() ?? string.Empty;
        return $"{ActiveTab.Id}::{statement}";
    }

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static double ClampResultsSheetHeight(double height)
    {
        return Math.Clamp(height, MinResultsSheetHeight, MaxResultsSheetHeight);
    }

    private void TryHydrateResultFilterForTab(SqlEditorTabState tab, bool force = false)
    {
        if (!force && tab.IsResultGridFilterHydratedFromSettings)
            return;

        string tabKey = BuildTabFilterSettingsKey(tab);
        string persistedFilter = AppSettingsStore.LoadSqlEditorResultFilter(tabKey);
        tab.ResultGridFilterText = persistedFilter;
        tab.IsResultGridFilterHydratedFromSettings = true;
    }

    private void PersistResultGridFilterForTab(SqlEditorTabState tab)
    {
        string tabKey = BuildTabFilterSettingsKey(tab);
        AppSettingsStore.SaveSqlEditorResultFilter(tabKey, tab.ResultGridFilterText);
    }

    private void TryHydrateExecutionHistoryForTab(SqlEditorTabState tab, bool force = false)
    {
        if (!force && tab.IsExecutionHistoryHydratedFromSettings)
            return;

        string? historyProfileKey = ResolveHistoryProfileKey(tab);
        if (string.IsNullOrWhiteSpace(historyProfileKey))
        {
            tab.IsExecutionHistoryHydratedFromSettings = true;
            return;
        }

        IReadOnlyList<SqlEditorHistoryEntry> persistedHistory = AppSettingsStore.LoadSqlEditorExecutionHistory(historyProfileKey);
        tab.ExecutionHistory = persistedHistory.Take(500).ToList();
        tab.IsExecutionHistoryHydratedFromSettings = true;
    }

    private void PersistExecutionHistoryForActiveProfileAsync()
    {
        string? historyProfileKey = ResolveHistoryProfileKey(ActiveTab);
        if (string.IsNullOrWhiteSpace(historyProfileKey))
            return;

        List<SqlEditorHistoryEntry> snapshot = ActiveTab.ExecutionHistory.Take(500).ToList();
        _ = Task.Run(() => AppSettingsStore.SaveSqlEditorExecutionHistory(historyProfileKey, snapshot));
    }

    private void ClearExecutionHistoryForActiveProfileAsync()
    {
        string? historyProfileKey = ResolveHistoryProfileKey(ActiveTab);
        if (string.IsNullOrWhiteSpace(historyProfileKey))
            return;

        _ = Task.Run(() => AppSettingsStore.ClearSqlEditorExecutionHistory(historyProfileKey));
    }

    private static string BuildTabFilterSettingsKey(SqlEditorTabState tab)
    {
        string anchor = !string.IsNullOrWhiteSpace(tab.FilePath)
            ? tab.FilePath
            : tab.FallbackTitle;
        return $"{tab.Provider}::{anchor}";
    }

    private string? ResolveHistoryProfileKey(SqlEditorTabState tab)
    {
        if (tab is null)
            return null;

        if (string.IsNullOrWhiteSpace(tab.ConnectionProfileId))
            return null;

        bool knownProfile = AvailableConnectionProfiles.Any(profile =>
            string.Equals(profile.Id, tab.ConnectionProfileId, StringComparison.Ordinal));
        if (!knownProfile)
            return null;

        return tab.ConnectionProfileId.Trim();
    }

    private void EnsureHistorySelection()
    {
        if (FilteredExecutionHistory.Count == 0)
        {
            SelectedExecutionHistoryEntry = null;
            return;
        }

        if (SelectedExecutionHistoryEntry is null || !FilteredExecutionHistory.Contains(SelectedExecutionHistoryEntry))
            SelectedExecutionHistoryEntry = FilteredExecutionHistory[0];
    }

    private static int ResolveHistorySelectionIndex(
        IReadOnlyList<SqlEditorHistoryEntry> history,
        SqlEditorHistoryEntry? selected)
    {
        if (selected is null)
            return -1;

        for (int i = 0; i < history.Count; i++)
        {
            SqlEditorHistoryEntry item = history[i];
            if (item == selected)
                return i;
        }

        return -1;
    }

    private void RestoreOrInitializeTabs(DatabaseProvider initialProvider, string? initialConnectionProfileId)
    {
        IReadOnlyList<SqlEditorSessionDraftEntry> drafts = _sessionDraftStore.LoadDrafts();
        if (drafts.Count == 0)
        {
            Tabs.Initialize(initialProvider, initialConnectionProfileId);
            return;
        }

        SqlEditorSessionDraftEntry firstDraft = drafts[0];
        Tabs.Initialize(firstDraft.Provider, firstDraft.ConnectionProfileId ?? initialConnectionProfileId);

        int activeIndex = 0;
        for (int i = 0; i < drafts.Count; i++)
        {
            SqlEditorSessionDraftEntry draft = drafts[i];
            SqlEditorTabState tab = i == 0
                ? Tabs.GetActiveTab()
                : Tabs.AddNewTab(draft.Provider, draft.ConnectionProfileId);

            tab.SqlText = draft.SqlText;
            tab.FilePath = draft.FilePath;
            if (!string.IsNullOrWhiteSpace(draft.FallbackTitle))
                tab.FallbackTitle = draft.FallbackTitle;
            tab.Provider = draft.Provider;
            tab.ConnectionProfileId = draft.ConnectionProfileId;
            tab.IsDirty = true;

            if (draft.IsActive)
                activeIndex = i;
        }

        Tabs.TryActivate(activeIndex);
        _sessionDraftStore.ClearDrafts();
        PublishStatus(L("sqlEditor.status.sessionDraftsRestored", "Rascunhos da sessao anterior restaurados."));
    }

    private void InitializeDraftAutoSaveTimers()
    {
        _draftAutoSaveDebounceTimer = new Timer(
            static state => ((SqlEditorViewModel)state!).OnDraftAutoSaveDebounceTick(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _draftAutoSaveForcedTimer = new Timer(
            static state => ((SqlEditorViewModel)state!).OnDraftAutoSaveForcedTick(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    private void EnsureDraftAutoSaveTimersStarted()
    {
        if (_draftAutoSaveTimersStarted)
            return;

        _draftAutoSaveTimersStarted = true;
        _draftAutoSaveForcedTimer?.Change(DraftAutoSaveInterval, DraftAutoSaveInterval);
    }

    private void OnDraftAutoSaveDebounceTick()
    {
        if (!_hasPendingDraftAutoSave)
            return;

        _hasPendingDraftAutoSave = false;
        PersistSessionDraftsSnapshot();
    }

    private void OnDraftAutoSaveForcedTick()
    {
        PersistSessionDraftsSnapshot();
    }

    private void PersistSessionDraftsSnapshot()
    {
        SqlEditorTabState[] tabsSnapshot;
        try
        {
            tabsSnapshot = [.. Tabs.Tabs];
        }
        catch (InvalidOperationException)
        {
            return;
        }

        List<SqlEditorSessionDraftEntry> drafts = tabsSnapshot
            .Select((tab, index) => new { tab, index })
            .Where(static item => item.tab.IsDirty)
            .Where(static item => !string.IsNullOrWhiteSpace(item.tab.SqlText))
            .Where(static item => item.tab.SqlText.Trim().Length >= DraftMinimumTextLength)
            .Select(item => new SqlEditorSessionDraftEntry
            {
                TabId = item.tab.Id,
                FallbackTitle = item.tab.FallbackTitle,
                SqlText = item.tab.SqlText,
                FilePath = item.tab.FilePath,
                Provider = item.tab.Provider,
                ConnectionProfileId = item.tab.ConnectionProfileId,
                TabOrder = item.index,
                IsActive = item.index == Tabs.ActiveTabIndex,
                SavedAtUtc = DateTimeOffset.UtcNow,
            })
            .ToList();

        if (drafts.Count == 0)
        {
            _sessionDraftStore.ClearDrafts();
            return;
        }

        _sessionDraftStore.SaveDrafts(drafts);
    }
}

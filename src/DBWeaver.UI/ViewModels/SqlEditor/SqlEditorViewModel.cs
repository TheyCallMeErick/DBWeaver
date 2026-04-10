using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;
using System.Data;
using System.IO;
using System.Windows.Input;

namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorViewModel : ViewModelBase
{
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
    private readonly ILocalizationService _localization;
    private readonly Func<ConnectionConfig?> _connectionConfigResolver;
    private readonly Func<string?, ConnectionConfig?> _connectionConfigByProfileIdResolver;
    private readonly Func<IReadOnlyList<SqlEditorConnectionProfileOption>> _connectionProfilesResolver;
    private readonly Func<ConnectionManagerViewModel?> _sharedConnectionManagerResolver;
    private readonly SqlCompletionProvider _completionProvider;
    private readonly Func<DbMetadata?> _metadataResolver;
    private CancellationTokenSource? _executionCts;
    private bool _isExecuting;
    private bool _hasExecutionError;
    private string _executionStatusText;
    private string? _executionDetailText;
    private bool _isResultsSheetOpen;
    private double _resultsSheetHeight;
    private MutationGuardResult? _pendingMutationGuard;
    private SqlMutationDiffPreview? _pendingMutationDiff;
    private string? _pendingMutationSql;
    private long? _pendingMutationEstimatedRows;

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
        Func<ConnectionManagerViewModel?>? sharedConnectionManagerResolver = null)
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
        _connectionConfigResolver = connectionConfigResolver ?? (() => null);
        _connectionConfigByProfileIdResolver = connectionConfigByProfileIdResolver ?? (_ => _connectionConfigResolver());
        _connectionProfilesResolver = connectionProfilesResolver ?? (() => []);
        _sharedConnectionManagerResolver = sharedConnectionManagerResolver ?? (() => null);
        _completionProvider = completionProvider ?? new SqlCompletionProvider();
        _metadataResolver = metadataResolver ?? (() => null);
        _executionStatusText = L("sqlEditor.status.ready", "Ready.");
        Tabs = new SqlEditorTabManagerViewModel(_localization);
        Tabs.Initialize(initialProvider, initialConnectionProfileId);
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
                return L("sqlEditor.connection.none", "No active connection.");

            return $"{config.Database} @ {config.Host}:{config.Port}";
        }
    }
    public string ActiveConnectionSubtitle
    {
        get
        {
            ConnectionConfig? config = ResolveConnectionConfigForActiveTab();
            if (config is null)
                return L("sqlEditor.connection.required", "Connect to a database to execute and inspect schema.");

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

            ResultsSheetHeight = value ? 260 : 0;
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
    public bool HasExecutionHistory => ExecutionHistory.Count > 0;
    public bool IsExecutionHistoryEmpty => !HasExecutionHistory;
    public string HistoryEmptyText => L("sqlEditor.history.empty", "Execute a query to start your history.");
    public string MessagesEmptyText => L("sqlEditor.messages.empty", "Messages will appear here after execution.");
    public IReadOnlyList<SqlEditorSchemaTableItem> SchemaTables => BuildSchemaTables();
    public bool HasSchemaTables => SchemaTables.Count > 0;
    public bool IsSchemaEmpty => !HasSchemaTables;
    public string SchemaEmptyText => L("sqlEditor.schema.empty", "No metadata available. Connect and refresh to view tables.");
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
    public string ResultsEmptyText => L("sqlEditor.results.empty", "Run a query to populate results.");
    public IReadOnlyList<SqlEditorHistoryEntry> ExecutionHistory => ActiveTab.ExecutionHistory;
    public SqlEditorExecutionTelemetry ExecutionTelemetry => ActiveTab.ExecutionTelemetry;
    public string ExecutionTelemetryText =>
        ExecutionTelemetry.StatementCount == 0
            ? L("sqlEditor.telemetry.none", "No execution telemetry yet.")
            : string.Format(
                L("sqlEditor.telemetry.summary", "Statements: {0}    Success: {1}    Failed: {2}    Total: {3} ms"),
                ExecutionTelemetry.StatementCount,
                ExecutionTelemetry.SuccessCount,
                ExecutionTelemetry.FailureCount,
                ExecutionTelemetry.TotalDurationMs);
    public string ExecutionTelemetryErrorsText =>
        ExecutionTelemetry.ErrorMessages.Count == 0
            ? L("sqlEditor.telemetry.errors.none", "No aggregated errors.")
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
    public string PendingMutationDiffText => PendingMutationDiff?.Message ?? L("sqlEditor.diff.none", "No transactional diff preview available.");
    public long? PendingMutationEstimatedRows => _pendingMutationEstimatedRows;
    public string PendingMutationEstimateText =>
        !HasPendingMutationConfirmation
            ? L("sqlEditor.mutation.estimate.none", "No mutation estimate available.")
            : _pendingMutationEstimatedRows.HasValue
                ? string.Format(
                    L("sqlEditor.mutation.estimate.value", "Estimated affected rows: {0}"),
                    _pendingMutationEstimatedRows.Value)
                : L("sqlEditor.mutation.estimate.unavailable", "Could not estimate affected rows automatically.");
    public bool HasPendingCloseTabConfirmation => _tabCloseWorkflowService.HasPendingConfirmation;
    public string PendingCloseTabMessage => _tabCloseWorkflowService.PendingMessage;
    public bool HasManyTabsWarning => EditorTabs.Count >= 15;
    public string ManyTabsWarningText => string.Format(
        L("sqlEditor.tab.manyWarning", "High tab count: {0} open tabs."),
        EditorTabs.Count);
    public IReadOnlyList<MutationGuardIssue> PendingMutationIssues => PendingMutationGuard?.Issues ?? [];
    public string? PendingMutationCountQuery => PendingMutationGuard?.CountQuery;
    public string PendingMutationMessage =>
        PendingMutationGuard is null
            ? L("sqlEditor.mutation.pending.none", "No pending mutation confirmation.")
            : L("sqlEditor.mutation.pending.required", "Mutation requires confirmation before execution.");
    public string LastExecutionMessage =>
        CurrentResult is null
            ? L("sqlEditor.message.empty", "Execute a statement to see messages.")
            : CurrentResult.ErrorMessage ?? L("sqlEditor.message.success", "Execution completed successfully.");
    public bool CanExportReport => CurrentResult is not null;
    public string ResultSummaryText
    {
        get
        {
            SqlEditorResultSet? result = CurrentResult;
            if (result is null)
                return L("sqlEditor.result.summary.empty", "Rows: -    Time: -");

            string rows = result.RowsAffected?.ToString() ?? "-";
            long ms = (long)Math.Round(result.ExecutionTime.TotalMilliseconds);
            return string.Format(
                L("sqlEditor.result.summary", "Rows: {0}    Time: {1} ms"),
                rows,
                ms);
        }
    }
    public bool IsExecuting
    {
        get => _isExecuting;
        private set => Set(ref _isExecuting, value);
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
        return _completionProvider.GetSuggestions(fullText, caretOffset, metadata, ActiveTabProvider);
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
        NotifyCommands();
        HasExecutionError = false;
        ExecutionStatusText = L("sqlEditor.status.executing", "Executing SQL...");
        ExecutionDetailText = null;

        try
        {
            string? sql = GetSqlForExecution(selectionStart, selectionLength, caretOffset);
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
            IsExecuting = false;
            NotifyCommands();
        }
    }

    public async Task<IReadOnlyList<SqlEditorResultSet>> ExecuteAllAsync(int maxRows = 1000)
    {
        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        IsExecuting = true;
        NotifyCommands();
        HasExecutionError = false;
        ExecutionStatusText = L("sqlEditor.status.executingScript", "Executing SQL script...");
        ExecutionDetailText = null;

        var results = new List<SqlEditorResultSet>();
        try
        {
            IReadOnlyList<SqlStatement> statements = _statementSplitter.Split(ActiveTab.SqlText);
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
                ExecutionStatusText = string.Format(
                    L("sqlEditor.status.executingStep", "Executing {0}/{1}..."),
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
            IsExecuting = false;
            NotifyCommands();
        }
    }

    public void CancelExecution()
    {
        _executionCts?.Cancel();
        if (IsExecuting)
            ExecutionStatusText = L("sqlEditor.status.canceling", "Canceling execution...");
    }

    public async Task<SqlEditorResultSet?> ConfirmPendingMutationAsync(int maxRows = 1000)
    {
        if (!HasPendingMutationConfirmation || string.IsNullOrWhiteSpace(_pendingMutationSql))
            return null;

        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        IsExecuting = true;
        NotifyCommands();
        HasExecutionError = false;
        ExecutionStatusText = L("sqlEditor.status.executingConfirmedMutation", "Executing confirmed mutation...");
        ExecutionDetailText = null;

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
            IsExecuting = false;
            NotifyCommands();
        }
    }

    public void CancelPendingMutation()
    {
        if (!HasPendingMutationConfirmation)
            return;

        ClearPendingMutation();
        ExecutionStatusText = L("sqlEditor.status.mutationCanceled", "Mutation execution canceled.");
        ExecutionDetailText = L("sqlEditor.detail.statementNotExecuted", "Statement was not executed.");
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
        ExecutionStatusText = L("sqlEditor.status.confirmationRequired", "Confirmation required before execution.");
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
        _resultStateService.AppendResult(ActiveTab, result);
        RaisePropertyChanged(nameof(CanExportReport));
        RaisePropertyChanged(nameof(ShouldShowResultsSheet));
    }

    private void UpdateExecutionTelemetry(IReadOnlyList<SqlEditorResultSet> results)
    {
        if (results.Count == 0)
            return;

        ActiveTab.ExecutionTelemetry = _resultStateService.BuildTelemetry(results);
    }

    private void AddNewTab()
    {
        Tabs.AddNewTab();
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
        SyncTabCommands();
        SyncDialectFromConnection();
        _propertyChangePublisher.PublishTabStateChanges(RaisePropertyChanged);
        RaiseSqlPanelPropertiesChanged();
        RaisePropertyChanged(nameof(CanExportReport));
        RaisePropertyChanged(nameof(ShouldShowResultsSheet));
        NotifyCommands();
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
        L("sqlEditor.error.mutationConfirmationRequired", "Mutation confirmation required.");

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
}

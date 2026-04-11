namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorPropertyChangePublisher
{
    private static readonly string[] SqlPanelProperties =
    [
        "ActiveTabProvider",
        "FallbackDialect",
        "AvailableConnectionProfiles",
        "ActiveTabConnectionProfile",
        "ActiveTabConnectionProfileId",
        "HasConnectionProfiles",
        "SharedConnectionManager",
        "HasSharedConnectionManager",
        "HasResolvedConnection",
        "ActiveConnectionDisplayName",
        "ActiveConnectionSubtitle",
        "HasActiveConnection",
        "HasNoActiveConnection",
        "IsProductionConnectionContext",
        "IsStagingConnectionContext",
        "IsNeutralConnectionContext",
        "ActiveConnectionContextBadgeText",
        "ActiveProviderStatusText",
        "CursorLine",
        "CursorColumn",
        "CursorPositionText",
        "IndentationStatusText",
        "ShowDialectSelector",
        "SchemaTables",
        "HasSchemaTables",
        "IsSchemaEmpty",
        "SchemaEmptyText",
        "ResultTabs",
        "SelectedResultTabIndex",
        "ResultRowsView",
        "HasHiddenResultColumns",
        "HiddenResultColumnsCount",
        "ShowAllColumnsButtonText",
        "HasHiddenColumnUndo",
        "UndoHiddenColumnButtonText",
        "ResultGridFilterText",
        "ResultGridSortColumn",
        "ResultGridSortAscending",
        "HasResultRows",
        "IsResultRowsEmpty",
        "ResultsEmptyText",
        "ExecutionHistory",
        "FilteredExecutionHistory",
        "HasExecutionHistory",
        "HasFilteredExecutionHistory",
        "IsExecutionHistoryEmpty",
        "IsFilteredExecutionHistoryEmpty",
        "HasHistorySearchNoResults",
        "HistoryFilterSummaryText",
        "SelectedExecutionHistoryEntry",
        "HasPendingHistoryClearConfirmation",
        "ClearHistoryButtonText",
        "HistoryEmptyText",
        "HistorySearchText",
        "ExecutionTelemetry",
        "ExecutionTelemetryText",
        "ExecutionTelemetryErrorsText",
        "CompletionTelemetry",
        "CompletionTelemetryText",
        "LastExecutionMessage",
        "MessagesEmptyText",
        "ResultSummaryText",
        "CanExecuteOrCancel",
        "ExecuteOrCancelButtonText",
        "ExecuteOrCancelTooltipText",
        "IsCancellationPending",
        "CursorPositionTooltipText",
        "ActiveExecutionStatementStartLine",
        "ActiveExecutionStatementEndLine",
        "HasActiveExecutionStatementRange",
        "IsResultsSheetOpen",
        "ResultsSheetHeight",
        "IsExplainRunning",
        "ExplainSummaryText",
        "ExplainRawOutput",
        "HasExplainRawOutput",
        "IsBenchmarkRunning",
        "BenchmarkProgressText",
        "BenchmarkSummaryText",
        "HasBenchmarkResult",
        "PendingMutationMessage",
        "PendingMutationIssues",
        "PendingMutationCountQuery",
        "PendingMutationEstimatedRows",
        "PendingMutationEstimateText",
        "PendingMutationDiff",
        "HasPendingMutationDiff",
        "PendingMutationDiffText",
    ];

    private static readonly string[] TabStateProperties =
    [
        "EditorTabs",
        "ActiveTab",
        "ActiveEditorTabIndex",
        "HasPendingCloseTabConfirmation",
        "PendingCloseTabMessage",
        "HasManyTabsWarning",
        "ManyTabsWarningText",
    ];

    public void PublishSqlPanelChanges(Action<string> raisePropertyChanged)
    {
        ArgumentNullException.ThrowIfNull(raisePropertyChanged);
        foreach (string property in SqlPanelProperties)
            raisePropertyChanged(property);
    }

    public void PublishTabStateChanges(Action<string> raisePropertyChanged)
    {
        ArgumentNullException.ThrowIfNull(raisePropertyChanged);
        foreach (string property in TabStateProperties)
            raisePropertyChanged(property);
    }
}

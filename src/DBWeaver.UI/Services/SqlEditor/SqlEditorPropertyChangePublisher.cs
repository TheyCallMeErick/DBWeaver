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
        "ShowDialectSelector",
        "SchemaTables",
        "HasSchemaTables",
        "IsSchemaEmpty",
        "SchemaEmptyText",
        "ResultTabs",
        "SelectedResultTabIndex",
        "ResultRowsView",
        "HasResultRows",
        "IsResultRowsEmpty",
        "ResultsEmptyText",
        "ExecutionHistory",
        "HasExecutionHistory",
        "IsExecutionHistoryEmpty",
        "HistoryEmptyText",
        "ExecutionTelemetry",
        "ExecutionTelemetryText",
        "ExecutionTelemetryErrorsText",
        "LastExecutionMessage",
        "MessagesEmptyText",
        "ResultSummaryText",
        "IsResultsSheetOpen",
        "ResultsSheetHeight",
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

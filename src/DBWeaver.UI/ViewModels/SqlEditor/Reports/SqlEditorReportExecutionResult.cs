namespace DBWeaver.UI.ViewModels;

public sealed record SqlEditorReportExecutionResult(
    long? RowCount,
    long? ExecutionTimeMs,
    string Status,
    string? ErrorMessage
);

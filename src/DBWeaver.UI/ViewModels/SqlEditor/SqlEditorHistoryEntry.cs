namespace DBWeaver.UI.ViewModels;

public sealed record SqlEditorHistoryEntry(
    string Sql,
    bool Success,
    long? RowsAffected,
    TimeSpan ExecutionTime,
    DateTimeOffset ExecutedAt
);

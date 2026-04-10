using DBWeaver.Core;
using System.Windows.Input;

namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorTabState
{
    public required string Id { get; init; }
    public required string FallbackTitle { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public bool IsDirty { get; set; }
    public DatabaseProvider Provider { get; set; }
    public string? ConnectionProfileId { get; set; }
    public SqlEditorResultSet? LastResult { get; set; }
    public IReadOnlyList<SqlEditorResultTab> ResultTabs { get; set; } = [];
    public int SelectedResultTabIndex { get; set; } = -1;
    public int ResultTabCounter { get; set; }
    public IReadOnlyList<SqlEditorHistoryEntry> ExecutionHistory { get; set; } = [];
    public SqlEditorExecutionTelemetry ExecutionTelemetry { get; set; } = SqlEditorExecutionTelemetry.Empty();
    public ICommand? CloseCommand { get; set; }
}

using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Shell.Models;

public sealed class QueryTabState
{
    public required string FallbackTitle { get; init; }
    public string? SnapshotJson { get; set; }
    public string? CurrentFilePath { get; set; }
    public bool IsDirty { get; set; }
    public List<ExplainHistoryState> ExplainHistory { get; set; } = [];
}

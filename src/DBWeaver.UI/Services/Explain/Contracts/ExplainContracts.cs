using DBWeaver.Core;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public enum ExplainFormat
{
    Text,
    Json,
    Xml,
}

public sealed record ExplainOptions(
    bool IncludeAnalyze = false,
    bool IncludeBuffers = false,
    ExplainFormat Format = ExplainFormat.Text
);

public sealed class ExplainNode
{
    public string NodeId { get; init; } = string.Empty;
    public string? ParentNodeId { get; init; }
    public string NodeType { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public string? RelationName { get; init; }
    public string? IndexName { get; init; }
    public string? Predicate { get; init; }
    public double? StartupCost { get; init; }
    public double? EstimatedCost { get; init; }
    public long? EstimatedRows { get; init; }
    public double? ActualStartupTimeMs { get; init; }
    public double? ActualTotalTimeMs { get; init; }
    public long? ActualLoops { get; init; }
    public long? ActualRows { get; init; }
    public int IndentLevel { get; init; }
    public bool IsExpensive { get; init; }
    public string AlertLabel { get; init; } = string.Empty;
}

public sealed record ExplainResult(
    IReadOnlyList<ExplainNode> Nodes,
    double? PlanningTimeMs,
    double? ExecutionTimeMs,
    string RawOutput,
    bool IsSimulated
);

public interface IExplainExecutor
{
    Task<ExplainResult> RunAsync(
        string sql,
        DatabaseProvider provider,
        ConnectionConfig? connectionConfig,
        ExplainOptions options,
        CancellationToken ct = default
    );
}




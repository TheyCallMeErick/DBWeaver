namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorCompletionTelemetry
{
    public int SampleCount { get; init; }
    public long LastDurationMs { get; init; }
    public long P95DurationMs { get; init; }
    public long BudgetMs { get; init; }

    public bool IsWithinBudget => P95DurationMs <= BudgetMs;

    public static SqlEditorCompletionTelemetry Empty(long budgetMs) => new()
    {
        SampleCount = 0,
        LastDurationMs = 0,
        P95DurationMs = 0,
        BudgetMs = budgetMs,
    };
}

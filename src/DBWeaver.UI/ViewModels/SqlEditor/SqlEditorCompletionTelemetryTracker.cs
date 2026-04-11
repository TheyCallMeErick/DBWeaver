namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorCompletionTelemetryTracker
{
    private readonly Queue<long> _samples = new();

    public SqlEditorCompletionTelemetryTracker(int maxSamples, long budgetMs)
    {
        if (maxSamples <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSamples));
        if (budgetMs < 0)
            throw new ArgumentOutOfRangeException(nameof(budgetMs));

        MaxSamples = maxSamples;
        BudgetMs = budgetMs;
    }

    public int MaxSamples { get; }
    public long BudgetMs { get; }

    public SqlEditorCompletionTelemetry Snapshot =>
        _samples.Count == 0
            ? SqlEditorCompletionTelemetry.Empty(BudgetMs)
            : new SqlEditorCompletionTelemetry
            {
                SampleCount = _samples.Count,
                LastDurationMs = _samples.Last(),
                P95DurationMs = ComputeP95(_samples),
                BudgetMs = BudgetMs,
            };

    public SqlEditorCompletionTelemetry AddSample(long durationMs)
    {
        long bounded = Math.Max(0, durationMs);
        _samples.Enqueue(bounded);
        if (_samples.Count > MaxSamples)
            _samples.Dequeue();

        return Snapshot;
    }

    private static long ComputeP95(IEnumerable<long> samples)
    {
        long[] ordered = samples.OrderBy(static sample => sample).ToArray();
        if (ordered.Length == 0)
            return 0;

        int index = (int)Math.Ceiling(0.95 * ordered.Length) - 1;
        index = Math.Clamp(index, 0, ordered.Length - 1);
        return ordered[index];
    }
}

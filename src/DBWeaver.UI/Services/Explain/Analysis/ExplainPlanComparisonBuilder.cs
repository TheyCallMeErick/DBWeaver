using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainPlanComparisonBuilder
{
    IReadOnlyList<ExplainComparisonRow> Build(ExplainSnapshot snapshotA, ExplainSnapshot snapshotB);
}

public sealed class ExplainPlanComparisonBuilder : IExplainPlanComparisonBuilder
{
    public IReadOnlyList<ExplainComparisonRow> Build(ExplainSnapshot snapshotA, ExplainSnapshot snapshotB)
    {
        ArgumentNullException.ThrowIfNull(snapshotA);
        ArgumentNullException.ThrowIfNull(snapshotB);

        var costAByOperation = snapshotA.Steps
            .GroupBy(s => s.Operation, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.EstimatedCost ?? 0d),
                StringComparer.OrdinalIgnoreCase
            );

        var costBByOperation = snapshotB.Steps
            .GroupBy(s => s.Operation, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.EstimatedCost ?? 0d),
                StringComparer.OrdinalIgnoreCase
            );

        string[] operations = costAByOperation.Keys
            .Concat(costBByOperation.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<ExplainComparisonRow>(operations.Length);
        foreach (string op in operations)
        {
            costAByOperation.TryGetValue(op, out double costA);
            costBByOperation.TryGetValue(op, out double costB);

            rows.Add(
                new ExplainComparisonRow
                {
                    Operation = op,
                    CostA = costA,
                    CostB = costB,
                }
            );
        }

        return rows
            .OrderByDescending(r => Math.Abs((r.DeltaPercent ?? 0) / 100.0))
            .ThenBy(r => r.Operation, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}




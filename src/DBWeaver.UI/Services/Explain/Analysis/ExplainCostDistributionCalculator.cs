using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainCostDistributionCalculator
{
    void Apply(IReadOnlyList<ExplainStep> steps);
}

public sealed class ExplainCostDistributionCalculator : IExplainCostDistributionCalculator
{
    public void Apply(IReadOnlyList<ExplainStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        foreach (ExplainStep step in steps)
            step.CostFraction = null;

        double? rootCost = ResolveRootCost(steps);
        if (!rootCost.HasValue || rootCost.Value <= 0)
            return;

        foreach (ExplainStep step in steps)
        {
            if (!step.EstimatedCost.HasValue || step.EstimatedCost.Value < 0)
                continue;

            double ratio = step.EstimatedCost.Value / rootCost.Value;
            step.CostFraction = Math.Clamp(ratio, 0, 1);
        }
    }

    private static double? ResolveRootCost(IReadOnlyList<ExplainStep> steps)
    {
        ExplainStep? rootStep = steps.FirstOrDefault(s =>
            s.IndentLevel == 0 &&
            s.EstimatedCost.HasValue &&
            s.EstimatedCost.Value > 0
        );

        if (rootStep?.EstimatedCost is > 0)
            return rootStep.EstimatedCost.Value;

        double maxCost = steps
            .Where(s => s.EstimatedCost.HasValue)
            .Select(s => s.EstimatedCost!.Value)
            .Where(v => v > 0)
            .DefaultIfEmpty(0)
            .Max();

        return maxCost > 0 ? maxCost : null;
    }
}




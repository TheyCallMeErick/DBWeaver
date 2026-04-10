using System.Globalization;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Services.Explain;

public sealed class ExplainStep
{
    public string NodeId { get; init; } = "";
    public string? ParentNodeId { get; init; }
    public int StepNumber { get; init; }
    public string Operation { get; init; } = "";
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
    public string AlertLabel { get; init; } = "";
    public double? CostFraction { get; set; }

    private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;

    public double IndentMargin => IndentLevel * 18.0;
    public string CostText => EstimatedCost.HasValue ? EstimatedCost.Value.ToString("F2", Inv) : "–";
    public string CostFractionText => CostFraction.HasValue ? (CostFraction.Value * 100).ToString("0.#", Inv) + "%" : "–";
    public string RowsText => EstimatedRows.HasValue ? EstimatedRows.Value.ToString("N0", Inv) : "–";
    public string ActualTimeText => ActualTotalTimeMs.HasValue ? ActualTotalTimeMs.Value.ToString("0.###", Inv) + " ms" : "–";
    public string ActualLoopsText => ActualLoops.HasValue ? ActualLoops.Value.ToString("N0", Inv) : "–";
    public double? RowsErrorRatio =>
        EstimatedRows.HasValue && ActualRows.HasValue
            ? Math.Abs((ActualRows.Value - EstimatedRows.Value) / (double)Math.Max(EstimatedRows.Value, 1))
            : null;
    public bool HasRowsError => RowsErrorRatio.HasValue;
    public string RowsErrorText => RowsErrorRatio.HasValue ? RowsErrorRatio.Value.ToString("0.#", Inv) + "x" : "–";
    public bool IsStaleStats => RowsErrorRatio is >= 10;
    public string RowsErrorColor => IsStaleStats ? UiColorConstants.C_A78BFA : UiColorConstants.C_6B7280;
    public bool HasStaleStatsBadge => IsStaleStats;
    public string StaleStatsLabel => "STALE STATS";
    public bool HasCostBar => CostFraction.HasValue && CostFraction.Value > 0;
    public double CostBarWidth => HasCostBar ? Math.Clamp(CostFraction!.Value * 96.0, 2.0, 96.0) : 0;
    public string CostBarFill => (CostFraction ?? 0) switch
    {
        >= 0.60 => UiColorConstants.C_F97316,
        >= 0.30 => UiColorConstants.C_F59E0B,
        > 0 => UiColorConstants.C_3B82F6,
        _ => UiColorConstants.C_1F2937,
    };

    public string AlertColor => AlertLabel switch
    {
        "SEQ SCAN" => UiColorConstants.C_FBBF24,
        "SORT" => UiColorConstants.C_FB923C,
        "HASH" => UiColorConstants.C_60A5FA,
        "LOOP" => UiColorConstants.C_A78BFA,
        _ => UiColorConstants.C_6B7280,
    };

    public bool HasAlert => !string.IsNullOrEmpty(AlertLabel);
}




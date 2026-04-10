using System.Globalization;
using DBWeaver.UI.ViewModels.Canvas;
using System.Text;

namespace DBWeaver.UI.Services.Explain;

public sealed record ExplainPlanExportData(
    string ProviderLabel,
    string Sql,
    IReadOnlyList<ExplainStep> Steps,
    double? PlanningTimeMs,
    double? ExecutionTimeMs,
    DateTimeOffset GeneratedAtUtc
);

public interface IExplainPlanExportFormatter
{
    string Format(ExplainPlanExportData data);
}

public sealed class ExplainPlanExportFormatter : IExplainPlanExportFormatter
{
    public string Format(ExplainPlanExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var sb = new StringBuilder(512);
        sb.AppendLine("EXPLAIN PLAN");
        sb.AppendLine($"Provider: {data.ProviderLabel}");
        sb.AppendLine($"GeneratedAtUtc: {data.GeneratedAtUtc:O}");
        sb.AppendLine();
        sb.AppendLine("SQL:");
        sb.AppendLine(data.Sql);
        sb.AppendLine();

        if (data.PlanningTimeMs.HasValue || data.ExecutionTimeMs.HasValue)
        {
            sb.AppendLine("Timings:");
            sb.AppendLine($"- Planning (ms): {FormatDouble(data.PlanningTimeMs)}");
            sb.AppendLine($"- Execution (ms): {FormatDouble(data.ExecutionTimeMs)}");
            sb.AppendLine();
        }

        sb.AppendLine("Plan:");
        if (data.Steps.Count == 0)
        {
            sb.AppendLine("- (no steps)");
            return sb.ToString();
        }

        foreach (ExplainStep step in data.Steps)
        {
            string indent = new(' ', Math.Max(0, step.IndentLevel) * 2);
            string cost = step.EstimatedCost.HasValue
                ? step.EstimatedCost.Value.ToString("0.##", CultureInfo.InvariantCulture)
                : "-";
            string rows = step.EstimatedRows.HasValue
                ? step.EstimatedRows.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
            string actualRows = step.ActualRows.HasValue
                ? step.ActualRows.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
            string alert = string.IsNullOrWhiteSpace(step.AlertLabel) ? "-" : step.AlertLabel;
            string detailSuffix = string.IsNullOrWhiteSpace(step.Detail) ? string.Empty : $" | {step.Detail}";

            sb.Append(indent);
            sb.Append("- ");
            sb.Append(step.Operation);
            sb.Append($" | cost={cost} | rows={rows} | actualRows={actualRows} | alert={alert}");
            sb.Append(detailSuffix);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatDouble(double? value) =>
        value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
}




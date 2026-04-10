using System.Text.RegularExpressions;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.Core;

namespace DBWeaver.UI.Services.Explain;

public sealed class ExplainIndexSuggestion
{
    public string Table { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = [];
    public string Reason { get; init; } = string.Empty;
    public string Sql { get; init; } = string.Empty;
    public string BadgeText => "INDEX SUGGESTION";
    public string ColumnsText => Columns.Count == 0 ? "-" : string.Join(", ", Columns);
}

public interface IExplainIndexSuggestionEngine
{
    IReadOnlyList<ExplainIndexSuggestion> Build(
        IReadOnlyList<ExplainStep> steps,
        DatabaseProvider provider
    );
}

public sealed partial class ExplainIndexSuggestionEngine : IExplainIndexSuggestionEngine
{
    public IReadOnlyList<ExplainIndexSuggestion> Build(
        IReadOnlyList<ExplainStep> steps,
        DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (provider != DatabaseProvider.Postgres || steps.Count == 0)
            return [];

        var suggestions = new List<ExplainIndexSuggestion>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ExplainStep step in steps)
        {
            if (!LooksSeqScan(step))
                continue;

            if (!TryGetTableAndFilter(step, out string table, out string filter))
                continue;

            IReadOnlyList<string> columns = ExtractFilterColumns(filter);
            if (columns.Count == 0)
                continue;

            string key = $"{table}|{string.Join(",", columns)}";
            if (!seenKeys.Add(key))
                continue;

            double baselineCost = step.EstimatedCost ?? 0;
            double projected = baselineCost * 0.1;
            string indexName = BuildIndexName(table, columns);
            string sql = $"CREATE INDEX CONCURRENTLY {indexName} ON {table} ({string.Join(", ", columns)});";
            string reason =
                $"Seq Scan com filtro em '{string.Join(", ", columns)}' — indice pode reduzir custo de {baselineCost:0.##} para ~{projected:0.##}.";

            suggestions.Add(
                new ExplainIndexSuggestion
                {
                    Table = table,
                    Columns = columns,
                    Reason = reason,
                    Sql = sql,
                }
            );
        }

        return suggestions;
    }

    private static bool LooksSeqScan(ExplainStep step)
    {
        if (step.AlertLabel.Equals("SEQ SCAN", StringComparison.OrdinalIgnoreCase))
            return true;

        return step.Operation.Contains("Seq Scan", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetTableAndFilter(ExplainStep step, out string table, out string filter)
    {
        table = string.Empty;
        filter = string.Empty;

        if (string.IsNullOrWhiteSpace(step.Detail))
            return false;

        Match relation = RelationPattern().Match(step.Detail);
        Match filterMatch = FilterPattern().Match(step.Detail);
        if (!relation.Success || !filterMatch.Success)
            return false;

        table = NormalizeIdentifier(relation.Groups["table"].Value);
        filter = filterMatch.Groups["filter"].Value.Trim();
        return table.Length > 0 && filter.Length > 0;
    }

    private static IReadOnlyList<string> ExtractFilterColumns(string filter)
    {
        var columns = new List<string>();
        foreach (Match match in ColumnPredicatePattern().Matches(filter))
        {
            string raw = match.Groups["col"].Value;
            string normalized = NormalizeIdentifier(raw);
            string column = normalized.Split('.').LastOrDefault() ?? string.Empty;
            if (column.Length > 0 && !columns.Contains(column, StringComparer.OrdinalIgnoreCase))
                columns.Add(column);
        }

        return columns;
    }

    private static string BuildIndexName(string table, IReadOnlyList<string> columns)
    {
        string shortTable = table.Split('.').LastOrDefault() ?? "table";
        string suffix = string.Join("_", columns.Select(c => c.ToLowerInvariant()));
        string candidate = $"idx_{shortTable.ToLowerInvariant()}_{suffix}";
        return candidate.Length <= 60 ? candidate : candidate[..60];
    }

    private static string NormalizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim().Trim('"').Trim('`').Trim('[', ']');
        if (normalized.Contains('.'))
        {
            string[] parts = normalized
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => p.Trim('"').Trim('`').Trim('[', ']'))
                .ToArray();
            return string.Join('.', parts);
        }

        return normalized;
    }

    [GeneratedRegex(@"relation\s*=\s*(?<table>[A-Za-z0-9_.""`\[\]]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RelationPattern();

    [GeneratedRegex(@"filter\s*=\s*(?<filter>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex FilterPattern();

    [GeneratedRegex(@"(?<col>[A-Za-z_][A-Za-z0-9_\.]*)\s*(=|<>|!=|>=|<=|>|<|LIKE|ILIKE|IN)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ColumnPredicatePattern();
}




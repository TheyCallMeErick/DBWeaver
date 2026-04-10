using System.Text.Json;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public sealed record PostgresParsedPlan(
    IReadOnlyList<ExplainNode> Nodes,
    double? PlanningTimeMs,
    double? ExecutionTimeMs
);

public interface IPostgresExplainPlanParser
{
    PostgresParsedPlan Parse(string rawJson);
}

public sealed class PostgresExplainPlanParser : IPostgresExplainPlanParser
{
    public PostgresParsedPlan Parse(string rawJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawJson);

        using JsonDocument doc = JsonDocument.Parse(rawJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException("Invalid PostgreSQL EXPLAIN JSON payload.");

        JsonElement root = doc.RootElement[0];
        if (!root.TryGetProperty("Plan", out JsonElement plan))
            throw new InvalidOperationException("PostgreSQL EXPLAIN JSON does not contain 'Plan'.");

        int nodeSequence = 0;
        var nodes = new List<ExplainNode>();
        AppendNodeRecursive(plan, parentNodeId: null, indentLevel: 0, nodes, ref nodeSequence);

        return new PostgresParsedPlan(
            nodes,
            PlanningTimeMs: TryGetDouble(root, "Planning Time"),
            ExecutionTimeMs: TryGetDouble(root, "Execution Time")
        );
    }

    private static void AppendNodeRecursive(
        JsonElement plan,
        string? parentNodeId,
        int indentLevel,
        List<ExplainNode> nodes,
        ref int nodeSequence)
    {
        string nodeType = TryGetString(plan, "Node Type") ?? "Plan Step";
        string? relation = TryGetString(plan, "Relation Name");
        string? indexName = TryGetString(plan, "Index Name");
        string? filter = TryGetString(plan, "Filter");
        string? hashCond = TryGetString(plan, "Hash Cond");
        string? mergeCond = TryGetString(plan, "Merge Cond");
        string? indexCond = TryGetString(plan, "Index Cond");
        string? recheckCond = TryGetString(plan, "Recheck Cond");
        string? joinFilter = TryGetString(plan, "Join Filter");

        string detail = BuildDetail(relation, indexName, filter, hashCond, mergeCond, indexCond, recheckCond, joinFilter);
        string alert = ResolveAlert(nodeType);
        string nodeId = $"pg-{++nodeSequence}";

        nodes.Add(
            new ExplainNode
            {
                NodeId = nodeId,
                ParentNodeId = parentNodeId,
                NodeType = nodeType,
                Detail = detail.Length == 0 ? null : detail,
                RelationName = relation,
                IndexName = indexName,
                Predicate = FirstNonEmpty(filter, hashCond, mergeCond, indexCond, recheckCond, joinFilter),
                StartupCost = TryGetDouble(plan, "Startup Cost"),
                EstimatedCost = TryGetDouble(plan, "Total Cost"),
                EstimatedRows = TryGetLong(plan, "Plan Rows"),
                ActualStartupTimeMs = TryGetDouble(plan, "Actual Startup Time"),
                ActualTotalTimeMs = TryGetDouble(plan, "Actual Total Time"),
                ActualLoops = TryGetLong(plan, "Actual Loops"),
                ActualRows = TryGetLong(plan, "Actual Rows"),
                IndentLevel = indentLevel,
                IsExpensive = alert.Length > 0,
                AlertLabel = alert,
            }
        );

        if (plan.TryGetProperty("Plans", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in children.EnumerateArray())
                AppendNodeRecursive(child, nodeId, indentLevel + 1, nodes, ref nodeSequence);
        }
    }

    private static string BuildDetail(
        string? relation,
        string? indexName,
        string? filter,
        string? hashCond,
        string? mergeCond,
        string? indexCond,
        string? recheckCond,
        string? joinFilter)
    {
        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(relation))
            parts.Add($"relation={relation}");
        if (!string.IsNullOrWhiteSpace(indexName))
            parts.Add($"index={indexName}");
        if (!string.IsNullOrWhiteSpace(filter))
            parts.Add($"filter={filter}");
        if (!string.IsNullOrWhiteSpace(hashCond))
            parts.Add($"hashCond={hashCond}");
        if (!string.IsNullOrWhiteSpace(mergeCond))
            parts.Add($"mergeCond={mergeCond}");
        if (!string.IsNullOrWhiteSpace(indexCond))
            parts.Add($"indexCond={indexCond}");
        if (!string.IsNullOrWhiteSpace(recheckCond))
            parts.Add($"recheckCond={recheckCond}");
        if (!string.IsNullOrWhiteSpace(joinFilter))
            parts.Add($"joinFilter={joinFilter}");

        return string.Join(" | ", parts);
    }

    private static string ResolveAlert(string nodeType)
    {
        if (nodeType.Contains("Seq Scan", StringComparison.OrdinalIgnoreCase))
            return "SEQ SCAN";
        if (nodeType.Contains("Sort", StringComparison.OrdinalIgnoreCase))
            return "SORT";
        if (nodeType.Contains("Hash Join", StringComparison.OrdinalIgnoreCase))
            return "HASH";

        return string.Empty;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;

        return null;
    }

    private static string? TryGetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out JsonElement value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static double? TryGetDouble(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double numeric))
            return numeric;

        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out double parsed))
            return parsed;

        return null;
    }

    private static long? TryGetLong(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long numeric))
            return numeric;

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out long parsed))
            return parsed;

        return null;
    }
}



